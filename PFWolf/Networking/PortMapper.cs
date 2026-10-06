using Mono.Nat;

namespace PFWolf.Networking;

/// <summary>
/// Asks the router to forward the game's UDP port to this machine (UPnP, or NAT-PMP/PCP), so
/// players on the internet can join without a forward set up by hand. One port at a time: the
/// mapping lasts <see cref="Lifetime"/> and is renewed while open, so a crashed game's mapping
/// runs out on its own; <see cref="Close"/> removes it when hosting ends.
/// </summary>
internal static class PortMapper
{
    public enum Outcome
    {
        /// <summary>The router forwards the port to this machine now</summary>
        Opened,
        /// <summary>No router answered UPnP or NAT-PMP (it's off, or not supported)</summary>
        NoRouter,
        /// <summary>A router answered but wouldn't map the port (taken by another machine?)</summary>
        Refused,
    }

    public sealed record Result(Outcome Outcome, int Port, string? Detail = null);

    static readonly TimeSpan DiscoverFor = TimeSpan.FromSeconds(4);
    const int Lifetime = 2 * 60 * 60;       // seconds
    static readonly TimeSpan RenewEvery = TimeSpan.FromMinutes(50);
    const string Description = "PFWolf";

    static readonly object gate = new();
    static INatDevice? device;
    static Mapping? mapping;
    static CancellationTokenSource? renewing;
    static bool exitHooked;
    static CancellationTokenSource? attempt;    // cancelled by Close while OpenAsync is still asking

    /// <summary>Whether the port is mapped right now</summary>
    public static bool IsOpen
    {
        get { lock (gate) return mapping != null; }
    }

    /// <summary>Finds the router and maps <paramref name="port"/> (closing any port mapped before)</summary>
    public static async Task<Result> OpenAsync(int port)
    {
        Close();
        if (!exitHooked)
        {
            exitHooked = true;
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Close();     // quitting while hosting
        }
        CancellationToken stillWanted;
        lock (gate)
        {
            attempt = new CancellationTokenSource();
            stillWanted = attempt.Token;
        }

        var router = await FindRouterAsync().ConfigureAwait(false);
        if (router == null)
            return new Result(Outcome.NoRouter, port);

        Mapping made;
        try
        {
            made = await router.CreatePortMapAsync(new Mapping(Protocol.Udp, port, port, Lifetime, Description)).ConfigureAwait(false);
        }
        catch (MappingException)
        {
            // Some UPnP routers only take permanent mappings (lifetime 0); Close still removes it
            try
            {
                made = await router.CreatePortMapAsync(new Mapping(Protocol.Udp, port, port, 0, Description)).ConfigureAwait(false);
            }
            catch (MappingException e)
            {
                return new Result(Outcome.Refused, port, e.ErrorText ?? e.Message);
            }
        }

        CancellationTokenSource? renew;
        lock (gate)
        {
            if (stillWanted.IsCancellationRequested)
            {
                // Hosting ended (Close) while the router was being asked: take it back down
                _ = router.DeletePortMapAsync(made);
                return new Result(Outcome.Refused, port, "no longer hosting");
            }
            device = router;
            mapping = made;
            renew = renewing = made.Lifetime > 0 ? new CancellationTokenSource() : null;
        }
        if (renew != null)
            _ = RenewAsync(router, made, renew.Token);
        return new Result(Outcome.Opened, port, router.NatProtocol == NatProtocol.Upnp ? "UPnP" : "NAT-PMP");
    }

    /// <summary>Removes the mapping, if there is one (waits a moment for the router: it's called on the way out)</summary>
    public static void Close()
    {
        INatDevice? router;
        Mapping? made;
        lock (gate)
        {
            router = device;
            made = mapping;
            renewing?.Cancel();
            attempt?.Cancel();
            attempt = null;
            device = null;
            mapping = null;
            renewing = null;
        }
        if (router == null || made == null)
            return;

        try
        {
            router.DeletePortMapAsync(made).Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // gone already, or the router went away: it runs out on its own
        }
    }

    static async Task<INatDevice?> FindRouterAsync()
    {
        var found = new TaskCompletionSource<INatDevice>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnFound(object? sender, DeviceEventArgs e) => found.TrySetResult(e.Device);

        NatUtility.DeviceFound += OnFound;
        try
        {
            NatUtility.StartDiscovery(NatProtocol.Upnp, NatProtocol.Pmp);
            var first = await Task.WhenAny(found.Task, Task.Delay(DiscoverFor)).ConfigureAwait(false);
            return first == found.Task ? found.Task.Result : null;
        }
        finally
        {
            NatUtility.DeviceFound -= OnFound;
            NatUtility.StopDiscovery();
        }
    }

    static async Task RenewAsync(INatDevice router, Mapping made, CancellationToken cancel)
    {
        try
        {
            while (true)
            {
                await Task.Delay(RenewEvery, cancel).ConfigureAwait(false);
                try
                {
                    await router.CreatePortMapAsync(new Mapping(Protocol.Udp, made.PrivatePort, made.PublicPort, Lifetime, Description))
                        .ConfigureAwait(false);
                }
                catch (MappingException)
                {
                    // tried again in a while
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}

using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace Wolf3D.Networking;

/// <summary>
/// Checks whether players on the internet can reach a game hosted here: from the game's own
/// port, asks a public STUN server (RFC 5780) where the internet sees us, then asks it to answer
/// from its other address and port. That answer is a stranger knocking on the port, as a
/// joining player would: it only gets in if the router forwards the port to this machine and the
/// firewall lets the game in. The port has to be free, so this runs before hosting, not during.
/// </summary>
/// <remarks>
/// A router that lets anything back in once this machine has sent out (a "full cone" NAT) also
/// passes, without a forward; joining then works only while the host keeps the mapping alive,
/// so "open" is very likely but not certain.
/// </remarks>
internal static class PortCheck
{
    public enum Outcome
    {
        /// <summary>A stranger's packet got in: others can join at <see cref="Result.PublicAddress"/></summary>
        Open,
        /// <summary>The server saw us, but its answer from elsewhere never arrived</summary>
        Closed,
        /// <summary>The port is taken here (a game hosted already?)</summary>
        InUse,
        /// <summary>No checking server answered: offline, or STUN blocked</summary>
        Unknown,
    }

    /// <summary>What was found: the public address and the port the router sent us out on, when a server answered</summary>
    public sealed record Result(Outcome Outcome, IPAddress? PublicAddress = null, int MappedPort = 0, string? Server = null);

    // Public servers that answer from a second address (RFC 5780 OTHER-ADDRESS), from the
    // always-online-stun NAT testing list; tried in turn until one does
    static readonly string[] Servers =
    [
        "stun.nextcloud.com", "stun.sonetel.com", "stun.voipgate.com", "stun.freeswitch.org",
        "stun.1und1.de", "stun.gmx.net", "stun.signalwire.com", "stun.easybell.de",
    ];
    const int StunPort = 3478;
    const int MaxServersTried = 4;

    const ushort BindingRequest = 0x0001, BindingSuccess = 0x0101, BindingError = 0x0111;
    const ushort AttrMappedAddress = 0x0001, AttrChangeRequest = 0x0003, AttrChangedAddress = 0x0005,
        AttrXorMappedAddress = 0x0020, AttrOtherAddress = 0x802C;
    const uint MagicCookie = 0x2112A442;
    const uint ChangeIpAndPort = 0x06;

    public static async Task<Result> RunAsync(int port, CancellationToken cancel = default)
    {
        UdpClient udp;
        try
        {
            udp = new UdpClient(new IPEndPoint(IPAddress.Any, port));
        }
        catch (SocketException)
        {
            return new Result(Outcome.InUse);
        }

        using (udp)
        {
            int tried = 0;
            foreach (var name in Servers)
            {
                if (tried >= MaxServersTried || cancel.IsCancellationRequested)
                    break;

                var server = await ResolveAsync(name, cancel).ConfigureAwait(false);
                if (server == null)
                    continue;
                tried++;

                // Where the internet sees us, and the server's other address
                var first = await AskAsync(udp, server, changeRequest: false, TimeSpan.FromMilliseconds(1500), cancel).ConfigureAwait(false);
                if (first == null || first.Refused || first.Mapped == null || first.Other == null)
                    continue;       // no answer, or a server that can't answer from elsewhere

                // The same server answering from its other address and port: unasked-for, as far as the router knows
                var second = await AskAsync(udp, server, changeRequest: true, TimeSpan.FromMilliseconds(2500), cancel).ConfigureAwait(false);
                if (second != null && (second.Refused || second.From.Equals(server)))
                    continue;       // it refused or ignored the change request: can't tell anything from it

                return new Result(second != null ? Outcome.Open : Outcome.Closed,
                    first.Mapped.Address, first.Mapped.Port, name);
            }
        }
        return new Result(Outcome.Unknown);
    }

    static async Task<IPEndPoint?> ResolveAsync(string name, CancellationToken cancel)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            var addresses = await Dns.GetHostAddressesAsync(name, AddressFamily.InterNetwork, timeout.Token).ConfigureAwait(false);
            return addresses.Length > 0 ? new IPEndPoint(addresses[0], StunPort) : null;
        }
        catch (Exception e) when (e is SocketException or OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>A server's answer; <paramref name="Refused"/> for an error answer (such as a change request it doesn't do)</summary>
    sealed record Answer(IPEndPoint From, IPEndPoint? Mapped, IPEndPoint? Other, bool Refused = false);

    /// <summary>Sends a Binding Request (a few times, in case one is lost) and waits for its answer</summary>
    static async Task<Answer?> AskAsync(UdpClient udp, IPEndPoint server, bool changeRequest, TimeSpan wait, CancellationToken cancel)
    {
        var transaction = RandomNumberGenerator.GetBytes(12);
        var request = BuildRequest(transaction, changeRequest);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeout.CancelAfter(wait);
        var resend = Task.Run(async () =>
        {
            try
            {
                for (int i = 0; i < 3 && !timeout.IsCancellationRequested; i++)
                {
                    await udp.SendAsync(request, server, timeout.Token).ConfigureAwait(false);
                    await Task.Delay(wait / 3, timeout.Token).ConfigureAwait(false);
                }
            }
            catch (Exception e) when (e is OperationCanceledException or SocketException)
            {
            }
        });

        try
        {
            while (true)
            {
                UdpReceiveResult received;
                try
                {
                    received = await udp.ReceiveAsync(timeout.Token).ConfigureAwait(false);
                }
                catch (SocketException)
                {
                    continue;       // an ICMP "unreachable" from an earlier send: keep listening
                }

                if (Parse(received.Buffer, transaction) is { } answer)
                    return answer with { From = received.RemoteEndPoint };
            }
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            timeout.Cancel();
            await resend.ConfigureAwait(false);
        }
    }

    static byte[] BuildRequest(byte[] transaction, bool changeRequest)
    {
        var message = new byte[20 + (changeRequest ? 8 : 0)];
        BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(0), BindingRequest);
        BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(2), (ushort)(message.Length - 20));
        BinaryPrimitives.WriteUInt32BigEndian(message.AsSpan(4), MagicCookie);
        transaction.CopyTo(message, 8);
        if (changeRequest)
        {
            BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(20), AttrChangeRequest);
            BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(22), 4);
            BinaryPrimitives.WriteUInt32BigEndian(message.AsSpan(24), ChangeIpAndPort);
        }
        return message;
    }

    /// <summary>The answer to <paramref name="transaction"/> (a Binding Success or Error), or null</summary>
    static Answer? Parse(byte[] message, byte[] transaction)
    {
        if (message.Length < 20
            || BinaryPrimitives.ReadUInt32BigEndian(message.AsSpan(4)) != MagicCookie
            || !message.AsSpan(8, 12).SequenceEqual(transaction))
            return null;
        ushort type = BinaryPrimitives.ReadUInt16BigEndian(message);
        if (type == BindingError)
            return new Answer(null!, null, null, Refused: true);
        if (type != BindingSuccess)
            return null;

        IPEndPoint? mapped = null, xorMapped = null, other = null;
        int end = Math.Min(message.Length, 20 + BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan(2)));
        for (int at = 20; at + 4 <= end;)
        {
            ushort attribute = BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan(at));
            int length = BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan(at + 2));
            if (at + 4 + length > end)
                break;
            var value = message.AsSpan(at + 4, length);
            switch (attribute)
            {
                case AttrMappedAddress: mapped = ReadAddress(value, xor: false); break;
                case AttrXorMappedAddress: xorMapped = ReadAddress(value, xor: true); break;
                case AttrOtherAddress:
                case AttrChangedAddress: other ??= ReadAddress(value, xor: false); break;
            }
            at += 4 + ((length + 3) & ~3);      // attributes are padded to 4 bytes
        }
        return new Answer(null!, xorMapped ?? mapped, other);
    }

    static IPEndPoint? ReadAddress(ReadOnlySpan<byte> value, bool xor)
    {
        if (value.Length < 8 || value[1] != 0x01)
            return null;        // IPv4 only: the game hosts on IPv4
        int port = BinaryPrimitives.ReadUInt16BigEndian(value[2..]);
        uint address = BinaryPrimitives.ReadUInt32BigEndian(value[4..]);
        if (xor)
        {
            port ^= (int)(MagicCookie >> 16);
            address ^= MagicCookie;
        }
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, address);
        return new IPEndPoint(new IPAddress(bytes), port);
    }
}

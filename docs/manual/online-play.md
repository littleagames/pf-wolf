# Playing online

Multiplayer games in PFWolf are hosted by one player, and everyone else connects to that
player. **Only the host has to be reachable from the internet.** Joining players never need to
change anything on their router.

## Hosting the usual way

The host's game uses **UDP port 10645**. Choose a different port with `--port N` on the command line.

1. Keep **Open Port** on in the Multiplayer menu (it's on by default). When you host, PFWolf asks
   your router to forward the port (UPnP or NAT-PMP), then removes the forward when the game ends.
2. Make sure Windows Firewall lets PFWolf in. Allow it on both private and public networks.
3. Use **Check Internet Port** (or `net_portcheck` in the console) before hosting. It says whether
   players on the internet can reach you, and at which address.
4. Others choose **Join a Game**, then **Type an address...**, and enter your public address. Add
   `:port` if you're not on 10645.

If your router doesn't support UPnP, forward UDP 10645 by hand to this computer's local address.

## Streaming or recording: Streamer Mode

Your internet address normally appears in the lobby, in Check Internet Port's answer and in the
console. If you're streaming or recording, turn on **Streamer Mode** in the Multiplayer menu
(`streamer-mode true` in `multiplayer.cfg`). It's off by default. With it on:

- Internet addresses show as **(hidden)** everywhere PFWolf shows them: the lobby, port-check
  answers, connection messages, errors, lobby chat and the console. Local addresses (`192.168.x.x`,
  `10.x.x.x`, and virtual-LAN ones like `100.x.y.z`) still show, since nobody outside your
  network can use them.
- Whatever you type into **Type an address...** shows as `*` characters, and any host name you
  typed is hidden too.
- When hosting, choose **Copy Address** in the lobby. It copies your join address to the
  clipboard without showing it, so you can paste it to friends in a private message.

Anything outside PFWolf can still show your address, such as a browser tab or your router's
admin page. Keep those off-screen.

## Without touching the router: a virtual LAN

When forwarding isn't possible, everyone can join one virtual private network instead. This
happens when the router won't do UPnP and you can't change its settings, or when your ISP puts
you behind carrier-grade NAT (common on mobile and some fiber connections). The VPN app gets
through the NAT for you, and PFWolf sees everyone as if they were on the same LAN. PFWolf itself
needs no changes.

Any of these free tools works:

| Tool | Notes |
| --- | --- |
| [Tailscale](https://tailscale.com) | Easiest setup: sign in with an existing account, then invite friends to your tailnet (or share your machine with them). Addresses look like `100.x.y.z`. |
| [ZeroTier](https://www.zerotier.com) | Create a network, share its 16-digit ID, and approve each member in the web console. |
| [Radmin VPN](https://www.radmin-vpn.com) | Windows only, no account needed. Create a network with a name and password, and friends join it. Addresses look like `26.x.y.z`. |

### Steps

1. **Everyone** installs the same tool and joins the same network.
2. **The host** finds their virtual address in the tool's window or tray icon. That's the
   `100.x`, `26.x` or ZeroTier-assigned address, not the `192.168.x.x` one.
   The lobby's "others join at" line may show the normal LAN address instead, so read the
   address from the VPN tool.
3. **The host** chooses **Host a Game** as usual.
4. **Everyone else** chooses **Join a Game**, then **Type an address...**, and enters the host's
   virtual address (for example `100.101.102.103`). The host may also appear in the list of
   games found on the LAN. Typing the address always works.

### Good to know

- **Check Internet Port doesn't apply here.** It tests your real router, so it may say
  "closed" even though virtual-LAN games work fine.
- **Windows Firewall can still block PFWolf.** Windows often marks the VPN adapter as a
  *public* network, so allow PFWolf on public networks too. If friends can't connect, check
  this first.
- **Speed:** the tools connect players directly when they can. If they can't, traffic goes
  through the tool's relay servers, which adds some latency. PFWolf sends little traffic, so
  games stay playable either way.
- Leave **Open Port** on or turn it off. It makes no difference to virtual-LAN players.

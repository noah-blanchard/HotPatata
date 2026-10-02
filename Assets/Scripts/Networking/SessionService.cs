using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;

namespace HotPatata
{
    /// <summary>
    /// Online sessions through Unity's Multiplayer Services: create a game (Relay-backed, no port forwarding),
    /// share a short code, join with that code, leave. The SDK starts and stops the Netcode host/client itself,
    /// so while a session exists NetworkManager.Shutdown must NOT be called; use <see cref="LeaveAsync"/>.
    /// </summary>
    public sealed class SessionService
    {
        public const int CodeLength = 6;
        public const int MaxPlayers = 4;

        bool initialised;

        public ISession Current { get; private set; }
        public bool InSession => Current != null;

        /// <summary>The players in the session changed (joined, left, properties).</summary>
        public event Action Changed;
        /// <summary>The session ended without us asking (host left, removed). Carries a readable reason.</summary>
        public event Action<string> Ended;

        // ------------------------------------------------------------------ pure helpers (unit tested)

        /// <summary>Codes are letters and digits; people type spaces, dashes and lower case.</summary>
        public static string NormalizeCode(string raw) =>
            new string((raw ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

        public static bool IsPlausibleCode(string normalized) => normalized != null && normalized.Length == CodeLength;

        /// <summary>A sentence a player can act on, instead of an exception message.</summary>
        public static string Describe(Exception e)
        {
            if (e is SessionException se)
            {
                switch (se.Error)
                {
                    case SessionError.SessionNotFound:
                    case SessionError.InvalidSessionIdentifier:
                        return "No game found with that code. Check it and try again.";
                    case SessionError.SessionDeleted:
                        return "That game has already ended.";
                    case SessionError.RateLimitExceeded:
                        return "Too many attempts. Wait a few seconds and try again.";
                    case SessionError.NotAuthorized:
                    case SessionError.Forbidden:
                        return "You are not allowed to join that game.";
                    case SessionError.InvalidParameter:
                        return "That code is not valid.";
                    case SessionError.NetworkManagerNotInitialized:
                    case SessionError.NetworkManagerStartFailed:
                    case SessionError.NetworkSetupFailed:
                        return "Could not start the network connection. Try again.";
                }
                if (Mentions(se.Message, "full")) return "That game is full.";
                if (Mentions(se.Message, "invalid character")) return "That code is not valid. Check it and try again.";
                return se.Message;
            }

            if (Mentions(e?.Message, "invalid character")) return "That code is not valid. Check it and try again.";

            if (e is ServicesInitializationException || e is RequestFailedException)
                return "Can't reach Unity's online services. Check your internet connection.";

            return e?.Message ?? "Something went wrong.";
        }

        static bool Mentions(string text, string fragment) =>
            text != null && text.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;

        // ------------------------------------------------------------------ actions

        public async Task EnsureSignedInAsync()
        {
            if (!initialised)
            {
                await UnityServices.InitializeAsync();
                initialised = true;
            }
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        /// <summary>The name shown for a lobby player: their <see cref="PlayerNames.SessionProperty"/>, sanitised.</summary>
        public static string NameOf(IReadOnlyPlayer player, int index)
        {
            string raw = player?.Properties != null && player.Properties.TryGetValue(PlayerNames.SessionProperty, out var p) ? p.Value : null;
            return PlayerNames.Sanitize(raw, index);
        }

        /// <summary>The typed name as a session player property, or none (the lobby then shows "Player N").</summary>
        static Dictionary<string, PlayerProperty> NameProperties(string playerName)
        {
            var properties = new Dictionary<string, PlayerProperty>();
            if (!string.IsNullOrWhiteSpace(playerName))
                properties[PlayerNames.SessionProperty] = new PlayerProperty(playerName, VisibilityPropertyOptions.Member);
            return properties;
        }

        public async Task<ISession> HostAsync(string playerName = null)
        {
            await EnsureSignedInAsync();
            var options = new SessionOptions
            {
                Name = "HotPatata",
                MaxPlayers = MaxPlayers,
                PlayerProperties = NameProperties(playerName)
            }.WithRelayNetwork();
            var session = await MultiplayerService.Instance.CreateSessionAsync(options);
            Attach(session);
            return session;
        }

        public async Task<ISession> JoinAsync(string rawCode, string playerName = null)
        {
            string code = NormalizeCode(rawCode);
            if (!IsPlausibleCode(code)) throw new ArgumentException($"A game code has {CodeLength} letters or digits.");

            await EnsureSignedInAsync();
            var options = new JoinSessionOptions { PlayerProperties = NameProperties(playerName) };
            var session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code, options);
            Attach(session);
            return session;
        }

        /// <summary>Leaves (client) or ends (host) the session. Safe to call when already gone.</summary>
        public async Task LeaveAsync()
        {
            var session = Current;
            if (session == null) return;
            Detach();

            try
            {
                if (session.IsHost) await session.AsHost().DeleteAsync();
                else await session.LeaveAsync();
            }
            catch (Exception)
            {
                // Already deleted or the connection is gone: nothing left to clean up.
            }
        }

        // ------------------------------------------------------------------ plumbing

        void Attach(ISession session)
        {
            Current = session;
            session.Changed += OnChanged;
            session.PlayerJoined += OnPlayer;
            session.PlayerHasLeft += OnPlayer;
            session.Deleted += OnDeleted;
            session.RemovedFromSession += OnRemoved;
            PatataLog.Run($"[Session] {(session.IsHost ? "hosting" : "joined")} code={session.Code} players={session.PlayerCount}");
        }

        void Detach()
        {
            var s = Current;
            Current = null;
            if (s == null) return;
            s.Changed -= OnChanged;
            s.PlayerJoined -= OnPlayer;
            s.PlayerHasLeft -= OnPlayer;
            s.Deleted -= OnDeleted;
            s.RemovedFromSession -= OnRemoved;
        }

        void OnChanged() => Changed?.Invoke();
        void OnPlayer(string playerId) => Changed?.Invoke();

        void OnDeleted()
        {
            Detach();
            Ended?.Invoke("The host ended the game.");
        }

        void OnRemoved()
        {
            Detach();
            Ended?.Invoke("You were removed from the game.");
        }
    }
}

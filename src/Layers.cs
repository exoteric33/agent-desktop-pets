using System;
using System.Collections.Generic;

namespace AiPets
{
    /// <summary>
    /// Fixed layers. Overlapping pets stack in menu order (the first pet in front), and the menus,
    /// dialogs and tooltips of aipets stay above every pet. Each pet moves only herself, directly
    /// below the lowest window she must not cover, and only when she is not there yet. Before, every
    /// pet jumped to the very top every few seconds: overlapping pets kept swapping places, and a pet
    /// could cover an open menu.
    /// </summary>
    static class Layers
    {
        public const int CycleMs = 3000, StepMs = 200, RetryMs = 100;

        /// <summary>A visible window of the topmost band.</summary>
        public struct Window
        {
            public IntPtr Handle;
            public int ProcessId;
            public string Title;
        }

        /// <summary>
        /// The window a pet belongs directly below, or IntPtr.Zero for the very top.
        /// band: the visible topmost windows from the top down. order: pet ids, the front one first.
        /// processes: this pet's and the tray's; every process with a pet window counts as well.
        /// Pets missing from the order never count as in front of her.
        /// </summary>
        public static IntPtr Below(List<Window> band, IntPtr self, string selfId, IList<string> order, ICollection<int> processes)
        {
            var aipets = new HashSet<int>(processes);
            foreach (Window w in band)
                if (PetId(w.Title) != null)
                    aipets.Add(w.ProcessId);

            int rank = order.IndexOf(selfId);
            IntPtr below = IntPtr.Zero;
            foreach (Window w in band)
            {
                if (w.Handle == self)
                    continue;
                string id = PetId(w.Title);
                bool stays = id == null
                    ? aipets.Contains(w.ProcessId)                          // a menu, dialog or tooltip of aipets
                    : order.IndexOf(id) >= 0 && order.IndexOf(id) < rank;   // a pet in front of her
                if (stays)
                    below = w.Handle;   // the band runs from the top down: the last one is the lowest
            }
            return below;
        }

        /// <summary>"claude" for the window title "aipets.pet.claude", null for any other title.</summary>
        public static string PetId(string title)
        {
            string prefix = Ipc.PetTitle("");
            return title != null && title.Length > prefix.Length && title.StartsWith(prefix, StringComparison.Ordinal)
                ? title.Substring(prefix.Length)
                : null;
        }

        /// <summary>
        /// When a pet checks her layer next (unix ms). All pets share one 3 s grid, the front pet first and every
        /// further pet 200 ms later: front to back, a single cycle puts all of them in place.
        /// </summary>
        public static long NextCheck(long nowMs, int rank)
        {
            long offset = Math.Max(0, rank) * StepMs;
            return ((nowMs - offset) / CycleMs + 1) * CycleMs + offset;
        }

        /// <summary>
        /// Puts the pet into her layer. Looking again after a move catches a menu that opened in between.
        /// False if windows kept moving and she could not check her place: try again soon.
        /// </summary>
        public static bool Restack(IntPtr self, string selfId, IList<string> order, ICollection<int> processes)
        {
            int moves = 0;
            for (int look = 0; look < 5 && moves < 2; look++)
            {
                List<Window> band = Native.TopmostWindows();
                if (band == null)
                    continue;   // windows moved during the walk: look again
                IntPtr below = Below(band, self, selfId, order, processes);
                if (Native.IsTopmost(self) && Native.VisibleWindowAbove(self) == below)
                    return true;
                Native.PlaceBelow(self, below);
                moves++;
            }
            return moves > 0;
        }
    }
}

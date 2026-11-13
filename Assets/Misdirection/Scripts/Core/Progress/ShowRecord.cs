using System;
using System.Collections.Generic;
using Misdirection.Core.Crowd;
using Misdirection.Core.Show;
using Misdirection.Core.Tricks;

namespace Misdirection.Core.Progress
{
    public enum ShowEventKind { Reveal, Bust }

    /// <summary>One thing that happened in a show, with spectators named by archetype (not by index).</summary>
    public struct ShowEvent
    {
        public ShowEventKind Kind;
        /// <summary>Act it happened in (0 = first act).</summary>
        public int Act;
        /// <summary>Trick being performed (0 = vanish and produce, 1 = coin under the cup).</summary>
        public int Trick;
        /// <summary>For a reveal: "vanish", "produce" or "cup". Null for a bust.</summary>
        public string Reveal;
        public Reaction Reaction;
        public int Tips;
        /// <summary>Archetype (as a number) of the spectator who saw the method, or <see cref="ShowRecord.Nobody"/>.</summary>
        public int SeenBy;
        /// <summary>Archetype (as a number) the coin was produced from, or <see cref="ShowRecord.Nobody"/>.</summary>
        public int ProducedFrom;
    }

    /// <summary>
    /// What happened in one show, act by act: every reveal and bust, and who was watching. The daily
    /// challenge, the trick book and the Regular's memory are all worked out from this. Pure data:
    /// the scene adds events as the tricks report them.
    /// </summary>
    public sealed class ShowRecord
    {
        public const int Nobody = -1;
        /// <summary>The reveal kind for a coin plucked from behind a spectator's ear.</summary>
        public const string Produce = "produce";

        private readonly List<ShowEvent> events = new List<ShowEvent>();
        private readonly int[] tricks;
        private readonly Archetype[][] crowds;

        /// <param name="acts">The show's acts as planned (which trick, in front of whom).</param>
        public ShowRecord(ShowAct[] acts)
        {
            acts = acts ?? new ShowAct[0];
            tricks = new int[acts.Length];
            crowds = new Archetype[acts.Length][];
            for (int i = 0; i < acts.Length; i++)
            {
                tricks[i] = acts[i] != null ? acts[i].Trick : Nobody;
                Archetype[] planned = acts[i] != null && acts[i].Crowd != null ? acts[i].Crowd : new Archetype[0];
                crowds[i] = (Archetype[])planned.Clone();
            }
        }

        public IList<ShowEvent> Events { get { return events; } }
        public int ActCount { get { return tricks.Length; } }

        /// <summary>
        /// A reveal landed. <paramref name="crowd"/> is who is actually standing there (a small room can
        /// fit fewer people than planned); the reveal's spectator indexes are looked up in it.
        /// </summary>
        public void AddReveal(int act, int trick, RevealResult reveal, IList<Archetype> crowd)
        {
            if (crowd != null && act >= 0 && act < crowds.Length)
            {
                Archetype[] actual = new Archetype[crowd.Count];
                crowd.CopyTo(actual, 0);
                crowds[act] = actual;
            }
            ShowEvent e = new ShowEvent();
            e.Kind = ShowEventKind.Reveal;
            e.Act = act;
            e.Trick = trick;
            e.Reveal = reveal.Kind;
            e.Reaction = reveal.Reaction;
            e.Tips = reveal.Tips;
            e.SeenBy = ArchetypeAt(crowd, reveal.SeenBy);
            e.ProducedFrom = reveal.Kind == Produce ? ArchetypeAt(crowd, reveal.Spectator) : Nobody;
            events.Add(e);
        }

        /// <summary>The method was exposed (coin in plain view, ditched too early, ...).</summary>
        public void AddBust(int act, int trick)
        {
            ShowEvent e = new ShowEvent();
            e.Kind = ShowEventKind.Bust;
            e.Act = act;
            e.Trick = trick;
            e.SeenBy = Nobody;
            e.ProducedFrom = Nobody;
            events.Add(e);
        }

        /// <summary>The trick planned for an act, or <see cref="Nobody"/> for an act that doesn't exist.</summary>
        public int TrickOf(int act)
        {
            return act >= 0 && act < tricks.Length ? tricks[act] : Nobody;
        }

        /// <summary>Who watched an act: the people actually there once a reveal has been seen, else the plan.</summary>
        public Archetype[] CrowdOf(int act)
        {
            return act >= 0 && act < crowds.Length ? crowds[act] : new Archetype[0];
        }

        public bool CrowdHas(int act, Archetype archetype)
        {
            return Array.IndexOf(CrowdOf(act), archetype) >= 0;
        }

        public int RevealsIn(int act)
        {
            return Count(act, ShowEventKind.Reveal, false, Reaction.None);
        }

        public int ApplauseIn(int act)
        {
            return Count(act, ShowEventKind.Reveal, true, Reaction.Applause);
        }

        public int HecklesIn(int act)
        {
            return Count(act, ShowEventKind.Reveal, true, Reaction.Heckle);
        }

        public int BustsIn(int act)
        {
            return Count(act, ShowEventKind.Bust, false, Reaction.None);
        }

        /// <summary>Archetype the coin was last produced from in this show, or <see cref="Nobody"/>.</summary>
        public int LastProducedFrom
        {
            get
            {
                for (int i = events.Count - 1; i >= 0; i--)
                {
                    if (events[i].Kind == ShowEventKind.Reveal && events[i].ProducedFrom != Nobody)
                    {
                        return events[i].ProducedFrom;
                    }
                }
                return Nobody;
            }
        }

        /// <summary>The trick the show ended on (the one in play at the last reveal or bust), or <see cref="Nobody"/>.</summary>
        public int EndTrick
        {
            get { return events.Count > 0 ? events[events.Count - 1].Trick : Nobody; }
        }

        private int Count(int act, ShowEventKind kind, bool matchReaction, Reaction reaction)
        {
            int n = 0;
            for (int i = 0; i < events.Count; i++)
            {
                ShowEvent e = events[i];
                if (e.Act == act && e.Kind == kind && (!matchReaction || e.Reaction == reaction))
                {
                    n++;
                }
            }
            return n;
        }

        private static int ArchetypeAt(IList<Archetype> crowd, int index)
        {
            return crowd != null && index >= 0 && index < crowd.Count ? (int)crowd[index] : Nobody;
        }
    }
}

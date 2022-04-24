using System;
using System.Linq;
using System.Collections.Generic;

namespace Celeste.Mod.Madhunt {
    public static class CopyExtensions {
        public static T[] Copy<T>(this T[] arr) => arr.ToArray();
        public static List<T> Copy<T>(this List<T> list) => new List<T>(list);
        public static HashSet<T> Copy<T>(this HashSet<T> set) => new HashSet<T>(set);

        public static T[] Copy<T>(this T[] arr, Func<T, T> copyFunc) => arr.Select(e => copyFunc(e)).ToArray();
        public static List<T> Copy<T>(this List<T> list, Func<T, T> copyFunc) => new List<T>(list.Select(e => copyFunc(e)));
        public static HashSet<T> Copy<T>(this HashSet<T> set, Func<T, T> copyFunc) => new HashSet<T>(set.Select(e => copyFunc(e)));

        public static Session.Counter Copy(this Session.Counter counter) => new Session.Counter() { Key = counter.Key, Value = counter.Value };
    }

    public class SessionSnapshot {
        private int dashes;
        private bool dreaming;
        private PlayerInventory inventory;
        private string colorGrade;
        private float lightingAlphaAdd, bloomBaseAdd, darkRoomAlpha;
        private bool grabbedGolden;
        private HashSet<string> flags, levelFlags;
        private List<Session.Counter> counters;
        private HashSet<EntityID> doNotLoad, keys;
        private bool[] summitGems;

        public SessionSnapshot(Session ses) => Take(ses);

        public void Take(Session ses) {
            dashes = ses.Dashes;
            dreaming = ses.Dreaming;
            inventory = ses.Inventory;
            colorGrade = ses.ColorGrade;
            lightingAlphaAdd = ses.LightingAlphaAdd;
            bloomBaseAdd = ses.BloomBaseAdd;
            darkRoomAlpha = ses.DarkRoomAlpha;
            grabbedGolden = ses.GrabbedGolden;
            flags = ses.Flags.Copy();
            levelFlags = ses.LevelFlags.Copy();
            counters = ses.Counters.Copy(c => c.Copy());
            doNotLoad = ses.DoNotLoad.Copy();
            keys = ses.Keys.Copy();
        }

        public void Apply(Session ses) {
            ses.Dashes = dashes;
            ses.Dreaming = dreaming;
            ses.Inventory = inventory;
            ses.ColorGrade = colorGrade;
            ses.LightingAlphaAdd = lightingAlphaAdd;
            ses.BloomBaseAdd = bloomBaseAdd;
            ses.DarkRoomAlpha = darkRoomAlpha;
            ses.GrabbedGolden = grabbedGolden;
            ses.Flags = flags.Copy();
            ses.LevelFlags = levelFlags.Copy();
            ses.Counters = counters.Copy(c => c.Copy());
            ses.DoNotLoad = doNotLoad.Copy();
            ses.Keys = keys.Copy();
        }
    }
}
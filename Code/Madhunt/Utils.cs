namespace Celeste.Mod.Madhunt {
    public static class AreaUtils {
        public static AreaKey ParseAreaKey(this string str) {
            string sid = str;
            AreaMode mode = AreaMode.Normal;

            int hashTagIndex = str.LastIndexOf('#');
            if(hashTagIndex >= 0 && hashTagIndex == str.Length-2) {
                switch(char.ToLower(str[hashTagIndex+1])) {
                    case 'a': str = str.Substring(hashTagIndex); mode = AreaMode.Normal; break;
                    case 'b': str = str.Substring(hashTagIndex); mode = AreaMode.BSide; break;
                    case 'c': str = str.Substring(hashTagIndex); mode = AreaMode.CSide; break;
                }
            }

            return new AreaKey() { SID = sid, Mode = mode };
        }
    }
}
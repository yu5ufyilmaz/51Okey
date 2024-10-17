// WARNING: Do not modify! Generated file.

namespace UnityEngine.Purchasing.Security {
    public class GooglePlayTangle
    {
        private static byte[] data = System.Convert.FromBase64String("KJoZOigVHhEynlCe7xUZGRkdGBuJgMG7BauhplGkkDVQcr5+n2Khn5OOuhVb7XsBfefqyFPK+5Kx1bRB5T/6SZOOrLbIPzgYV+BOQy3cwp958/6EU/ekoXOt21wSm0eM4Fj2XnD5Pu3IhO8lrHx5jAgGMt5ffiyMmhkXGCiaGRIamhkZGIh1x05Rip5UGg/RGmj6SV4pOEVTVXgDuz3t4BC82ZdkeUbhgCGbr1/UnI9iDngLeJr5yMi7sfjLY6Fr6jpoePxhAhR4JCvqFPrP6F6zz5/AIETNwi+392PTL4yvkXHDwnjHkcJKafL545oSVsbT4AZOO0NoWZNJ+vI/LPyZzAF8bRLIYe1+M6C+msGakRsenOqA4GOEGmrT2ZE19xobGRgZ");
        private static int[] order = new int[] { 0,7,6,4,12,5,7,10,13,11,10,11,12,13,14 };
        private static int key = 24;

        public static readonly bool IsPopulated = true;

        public static byte[] Data() {
        	if (IsPopulated == false)
        		return null;
            return Obfuscator.DeObfuscate(data, order, key);
        }
    }
}

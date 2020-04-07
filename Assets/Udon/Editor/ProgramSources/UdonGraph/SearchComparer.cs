using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRC.Udon.Editor.ProgramSources
{
    internal class SearchComparer : IComparer<string>
    {
        public SearchComparer(string searchString)
        {
            _searchString = searchString;
        }
        private readonly string _searchString;
        public int Compare(string x, string y)
        {
            if (x == null || y == null)
            {
                return 0;
            }
            //-1 is they're out of order, 0 is order doesn't matter, 1 is they're in order

            x = x.ReplaceFirst("const ", "");
            y = y.ReplaceFirst("const ", "");
            
            int xIndex = x.IndexOf(_searchString, StringComparison.InvariantCultureIgnoreCase);
            int yIndex = y.IndexOf(_searchString, StringComparison.InvariantCultureIgnoreCase);
            int compareIndex = xIndex.CompareTo(yIndex);
            if (compareIndex != 0) return compareIndex;
            
            string xDiff = x.ReplaceFirst(_searchString, "");
            string yDiff = y.ReplaceFirst(_searchString, "");
            return string.Compare(xDiff, yDiff, StringComparison.InvariantCultureIgnoreCase);
        }
    }
}

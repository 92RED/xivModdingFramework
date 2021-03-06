﻿// xivModdingFramework
// Copyright © 2018 Rafael Gonzalez - All Rights Reserved
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System.IO;
using xivModdingFramework.General.Enums;
using xivModdingFramework.SqPack.FileTypes;

namespace xivModdingFramework.Helpers
{
    public class ProblemChecker
    {
        private readonly DirectoryInfo _gameDirectory;

        public ProblemChecker(DirectoryInfo gameDirectory)
        {
            _gameDirectory = gameDirectory;
        }

        /// <summary>
        /// Checks the index for the number of dats the game will attempt to read
        /// </summary>
        /// <returns>True if there is a problem, False otherwise</returns>
        public bool CheckIndexDatCounts(XivDataFile dataFile)
        {
            var index = new Index(_gameDirectory);
            var dat = new Dat(_gameDirectory);

            var indexDatCounts = index.GetIndexDatCount(dataFile);
            var largestDatNum = dat.GetLargestDatNumber(dataFile) + 1;

            if (indexDatCounts.Index1 != largestDatNum)
            {
                return true;
            }

            if (indexDatCounts.Index2 != largestDatNum)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Repairs the dat count in the index files
        /// </summary>
        public void RepairIndexDatCounts(XivDataFile dataFile)
        {
            var index = new Index(_gameDirectory);
            var dat = new Dat(_gameDirectory);

            var largestDatNum = dat.GetLargestDatNumber(dataFile);

            index.UpdateIndexDatCount(dataFile, largestDatNum);
        }
    }
}
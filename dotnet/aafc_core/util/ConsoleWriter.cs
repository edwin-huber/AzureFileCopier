using System;
using System.Collections.Generic;
using System.Text;

namespace aafccore.util
{
    internal class ConsoleWriter
    {
        protected static int origRow;
        protected static int origCol;

        internal ConsoleWriter(int col, int row)
        {
            origCol = col;
            origRow = row;
            
        }

        /// <summary>
        /// Based on sample from https://docs.microsoft.com/en-us/dotnet/api/system.console.setcursorposition?view=netcore-3.1
        /// </summary>
        /// <param name="s"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        internal void WriteAt(string s, int x, int y)
        {
            try
            {
                Console.SetCursorPosition(origCol + x, origRow + y);
                Console.Write(s);
            }
            catch (ArgumentOutOfRangeException e)
            {
                Console.Clear();
                Console.WriteLine(e.Message);
            }
        }
    }
}

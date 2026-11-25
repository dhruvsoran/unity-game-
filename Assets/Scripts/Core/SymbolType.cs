using UnityEngine;

namespace LuckySeven.Slot
{
    /// <summary>
    /// The four symbol kinds used by the machine. The numeric order matches the
    /// supplied artwork (slot-symbol1.png .. slot-symbol4.png).
    /// </summary>
    public enum SymbolType
    {
        LuckySeven = 0,
        Cherry = 1,
        Bell = 2,
        Bar = 3
    }

    /// <summary>
    /// Mapping between gameplay symbols and the provided sprite files.
    /// Kept in one place so artwork can be swapped without touching game logic.
    /// </summary>
    public static class SymbolArtwork
    {
        public const string Folder = "Sprites/";

        public static string ResourcePath(SymbolType symbol)
        {
            switch (symbol)
            {
                case SymbolType.LuckySeven: return Folder + "slot-symbol1";
                case SymbolType.Cherry: return Folder + "slot-symbol2";
                case SymbolType.Bell: return Folder + "slot-symbol3";
                case SymbolType.Bar: return Folder + "slot-symbol4";
                default: return Folder + "slot-symbol1";
            }
        }

        public static string DisplayName(SymbolType symbol)
        {
            switch (symbol)
            {
                case SymbolType.LuckySeven: return "LUCKY 7";
                case SymbolType.Cherry: return "CHERRY";
                case SymbolType.Bell: return "BELL";
                case SymbolType.Bar: return "BAR";
                default: return "?";
            }
        }
    }
}

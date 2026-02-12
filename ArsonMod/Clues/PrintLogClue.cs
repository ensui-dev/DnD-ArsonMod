using UnityEngine;

namespace ArsonMod.Clues
{
    /// <summary>
    /// Manages the printer's "Recent Jobs" log display.
    /// After the arsonist completes Task 2 (excessive print), the log
    /// shows an unusually large print job. Players who inspect the printer
    /// can see this and use the timing to narrow down suspects.
    /// </summary>
    public class PrintLogClue : MonoBehaviour
    {
        private Tasks.PrintDocumentsTask[] _printers;

        private void Start()
        {
            _printers = FindObjectsOfType<Tasks.PrintDocumentsTask>();
        }

        /// <summary>
        /// Called when a player interacts with a printer's log screen.
        /// Returns the formatted log entries for display.
        /// </summary>
        public string GetPrintLogForPrinter(string printerId)
        {
            foreach (var printer in _printers)
            {
                if (printer.PrinterId != printerId) continue;

                string excessiveEntry = printer.GetPrintLogEntry();
                if (excessiveEntry == null) return GetNormalLogEntries();

                // Insert the suspicious entry among normal-looking entries
                return FormatLogWithSuspiciousEntry(excessiveEntry);
            }

            return GetNormalLogEntries();
        }

        private string GetNormalLogEntries()
        {
            return "RECENT PRINT JOBS:\n" +
                   "  DOC - 3 pages\n" +
                   "  DOC - 1 page\n" +
                   "  DOC - 7 pages\n" +
                   "  DOC - 2 pages";
        }

        private string FormatLogWithSuspiciousEntry(string excessiveEntry)
        {
            // The suspicious entry stands out among normal 1-15 page jobs
            return "RECENT PRINT JOBS:\n" +
                   "  DOC - 3 pages\n" +
                   $"  {excessiveEntry}\n" +  // e.g., "PRINT JOB - 847 pages"
                   "  DOC - 1 page\n" +
                   "  DOC - 5 pages";
        }
    }
}

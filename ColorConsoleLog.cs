using static System.Console;

namespace YTnew
{
   internal class ColorConsoleLog
   {
      // --------------------------------------------------------------------------------------------------------------------------------

      private ConsoleColor clr;

      // --------------------------------------------------------------------------------------------------------------------------------

      public ColorConsoleLog() => clr = ForegroundColor;

      // --------------------------------------------------------------------------------------------------------------------------------

      public bool LogVerbose { get; set; }

      // --------------------------------------------------------------------------------------------------------------------------------

      public void Verbose(string text)
      {
         if (!LogVerbose) return;
         ForegroundColor = ConsoleColor.Blue;
         text = "[Verbose] " + text;
         WriteLine(text);
         System.Diagnostics.Trace.WriteLine(text);
         ForegroundColor = clr;
      }

      public void Warning(string text)
      {
         ForegroundColor = ConsoleColor.Yellow;
         text = "[Warning] " + text;
         WriteLine(text);
         System.Diagnostics.Trace.WriteLine(text);
         ForegroundColor = clr;
      }

      public void Error(string text)
      {
         ForegroundColor = ConsoleColor.Red;
         text = "[Error] " + text;
         WriteLine(text);
         System.Diagnostics.Trace.WriteLine(text);
         ForegroundColor = clr;
      }

      // --------------------------------------------------------------------------------------------------------------------------------
   }
}

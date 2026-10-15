namespace Gothic.Core.Models.UI
{
    /// <summary>
    /// Text shown on screen for some seconds, like Daedalus' PrintScreen().
    /// Positions are percent of the screen (0-100), -1 centers the text on that axis.
    /// </summary>
    public class PrintScreenMessage
    {
        public const int Centered = -1;

        public string Text;
        public int PosX;
        public int PosY;
        public string Font;
        public float Seconds;

        public PrintScreenMessage(string text, int posX, int posY, string font, float seconds)
        {
            Text = text;
            PosX = posX;
            PosY = posY;
            Font = font;
            Seconds = seconds;
        }
    }
}

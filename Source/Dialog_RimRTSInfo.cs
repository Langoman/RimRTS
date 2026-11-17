using UnityEngine;
using Verse;

namespace RimRTS
{
    /// <summary>
    /// A small dialog drawn on top of the title screen.
    ///
    /// Extending Verse.Window gives us a free, reusable frame: the close X,
    /// click-outside-to-close, keyboard focus, and the game's UI scaling.
    /// All we write is the content of DoWindowContents.
    /// </summary>
    public class Dialog_RimRTSInfo : Window
    {
        private const string Intro =
            "RimRTS is a total-conversion mod that turns RimWorld into a real-time strategy game: " +
            "squads, direct unit control, and skirmish matches instead of colony management.";

        private const string Roadmap =
            "Currently in development. First milestone: a skirmish vertical slice — " +
            "pick a team, drop into a map, fight, win or lose, restart.";

        private const string Footer =
            "v0.1.0 · github.com/Langoman/RimRTS";

        public Dialog_RimRTSInfo()
        {
            doCloseButton = false;        // we draw our own Close button below
            doCloseX = true;              // ...but keep the X in the corner too
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
        }

        // The window has no automatic sizing: tell it how big to be.
        public override Vector2 InitialSize => new Vector2(560f, 400f);

        public override void DoWindowContents(Rect inRect)
        {
            // Note: (0,0) here is the top-left *inside* the window, not the screen.
            float y = 0f;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, y, inRect.width, 32f), "RimRTS");
            Text.Font = GameFont.Small;
            y += 40f;

            // Widgets.Label wraps text to the rect width, but never grows the rect,
            // so we measure each paragraph first and advance y by its height.
            float introHeight = Text.CalcHeight(Intro, inRect.width);
            Widgets.Label(new Rect(0f, y, inRect.width, introHeight), Intro);
            y += introHeight + 12f;

            float roadmapHeight = Text.CalcHeight(Roadmap, inRect.width);
            Widgets.Label(new Rect(0f, y, inRect.width, roadmapHeight), Roadmap);
            y += roadmapHeight + 20f;

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(1f, 1f, 1f, 0.55f);
            Widgets.Label(new Rect(0f, y, inRect.width, 20f), Footer);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            // Bottom-centred Close button.
            Rect buttonRect = new Rect((inRect.width - 120f) / 2f, inRect.height - 34f, 120f, 32f);
            if (Widgets.ButtonText(buttonRect, "Close"))
            {
                Close();
            }
        }
    }
}

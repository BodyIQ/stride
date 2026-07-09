using Android.App;
using Android.Content.PM;
using Stride.Engine;
using Stride.Starter;

namespace MyTemplate.Android;

[Activity(MainLauncher = true,
          Label = "MyTemplate",
          ScreenOrientation = ScreenOrientation.Landscape,
          Theme = "@android:style/Theme.NoTitleBar.Fullscreen",
          ConfigurationChanges = ConfigChanges.UiMode | ConfigChanges.Orientation | ConfigChanges.KeyboardHidden | ConfigChanges.ScreenSize)]
public class MainActivity : StrideActivity
{
    private Game? game;

    protected override void OnRun()
    {
        base.OnRun();

        game = GameApplication.CreateGame();
        GameApplication.Run(game, GameContext);
    }

    protected override void OnDestroy()
    {
        game?.Dispose();

        base.OnDestroy();
    }
}

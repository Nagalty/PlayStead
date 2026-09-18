using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Media;
using Xunit.Abstractions;

namespace PlayStead.UI.Tests.Themes;

public sealed class GeistPhysicalResolutionTests(ITestOutputHelper output)
{
    [Fact]
    public void Sans_token_resolves_to_the_embedded_Geist_faces()
    {
        RunSta(() =>
        {
            var tokens = (ResourceDictionary)Application.LoadComponent(
                new Uri(
                    "/PlayStead.UI;component/Themes/PlaySteadTokens.xaml",
                    UriKind.Relative));
            var family = Assert.IsType<FontFamily>(tokens["PlayStead.Font.Sans"]);

            ResolveAndAssert(family, FontWeights.Normal, "Normal");
            ResolveAndAssert(family, FontWeights.SemiBold, "SemiBold");
            ResolveAndAssert(family, FontWeights.Bold, "Bold");
        });

        void ResolveAndAssert(FontFamily fontFamily, FontWeight weight, string requestedFace)
        {
            var typeface = new Typeface(
                fontFamily,
                FontStyles.Normal,
                weight,
                FontStretches.Normal);

            Assert.True(
                typeface.TryGetGlyphTypeface(out var glyphTypeface),
                $"Could not resolve embedded Geist for {requestedFace}.");

            var familyName = glyphTypeface.FamilyNames.Values.FirstOrDefault() ?? string.Empty;
            var faceName = glyphTypeface.FaceNames.Values.FirstOrDefault() ?? string.Empty;
            output.WriteLine(
                $"requested={requestedFace}; family={familyName}; face={faceName}; uri={glyphTypeface.FontUri}");

            Assert.Equal("Geist", familyName);
            Assert.Equal(requestedFace == "Normal" ? "Regular" : requestedFace, faceName);
        }
    }

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}

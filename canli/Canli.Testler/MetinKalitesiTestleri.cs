using Canli.Mantik;
using Xunit;

namespace Canli.Testler
{
    /// <summary>Örnekler Metro Exodus denemesinin tanı kaydından (altyazı belirirken/kaybolurken bozuk okumalar).</summary>
    public class MetinKalitesiTestleri
    {
        [Theory]
        [InlineData("D$s�yg*��l FLAT")]
        [InlineData("ad&ys helllx�a cr�h%h F �f")]
        [InlineData("u s-;thVf whp?:�-")]
        [InlineData("'Becaus�.,- i not rune")]
        [InlineData("Ugh, Igill hele ms4JC'v out*l went up with you. en -to a Ha*bff-limits zone!")]
        [InlineData("There's just no way anything hete\\but4'h1bnst�rs...")]
        [InlineData("Movewi_t. wed0T nedd agy.e- Iti a i' tion, d rl")]
        public void bozuk_okuma_gonderilmez(string metin)
        {
            Assert.False(MetinKalitesi.Yeterli(metin), metin);
        }

        [Theory]
        [InlineData("Move it, we don't need any extra radiation, darling.")]
        [InlineData("Dad says he'll arrange a FLAT for us!")]
        [InlineData("Have you been to all these ruins?")]
        [InlineData("ANNA: Don't you think I'd also love to live on an ocean beach or in a green forest?")]
        [InlineData("See all the destruction? And Moscow had the best anti-air defenses in the country!")]
        [InlineData("Wave 2 of 4")]
        [InlineData("We have t0 go now, Enz0.")]
        [InlineData("ANNA: NO! A-a-a-a! Artyom!!!")]
        [InlineData("OFFICER: Base, patrol 72 here. Picked up two more individuals at the railroad bridge.")]
        [InlineData("for us!\"")]
        public void temiz_okuma_gonderilir(string metin)
        {
            Assert.True(MetinKalitesi.Yeterli(metin), metin);
        }
    }
}

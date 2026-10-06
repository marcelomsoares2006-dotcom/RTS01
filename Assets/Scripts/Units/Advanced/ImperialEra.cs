namespace EraImperial.Units.Advanced
{
    /// <summary>Eras do jogo, em ordem cronológica. Os valores são estáveis (serializados nos assets).</summary>
    public enum ImperialEra
    {
        Ancient = 0,
        Classical = 1,
        Medieval = 2,
        Imperial = 3
    }

    public static class ImperialEras
    {
        public static readonly ImperialEra[] Ordered =
        {
            ImperialEra.Ancient,
            ImperialEra.Classical,
            ImperialEra.Medieval,
            ImperialEra.Imperial
        };
    }
}

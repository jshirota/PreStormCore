namespace PreStormCore
{
    public class KmlStyle
    {
        public string IconUrl { get; }
        public string IconColour { get; }
        public double IconScale { get; }
        public string LineColour { get; }
        public double LineWidth { get; }
        public string PolygonColour { get; }

        public KmlStyle(string? iconUrl = null, string? iconColour = null, double iconScale = 1.1, string? lineColour = null, double lineWidth = 1.2, string? polygonColour = null)
        {
            IconUrl = iconUrl ?? "http://maps.google.com/mapfiles/kml/pushpin/ylw-pushpin.png";
            IconColour = iconColour ?? "ffffffff";
            IconScale = iconScale;
            LineColour = lineColour ?? "ffffffff";
            LineWidth = lineWidth;
            PolygonColour = polygonColour ?? "ffffffff";
        }

        public override bool Equals(object? obj)
        {
            if (this == obj)
                return true;

            if (obj is not KmlStyle s)
                return false;

            return s.IconUrl == IconUrl
                && s.IconColour == IconColour
                && s.IconScale == IconScale
                && s.LineColour == LineColour
                && s.LineWidth == LineWidth
                && s.PolygonColour == PolygonColour;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var n = 23;
                var hash = 17;

                hash = hash * n + IconUrl.GetHashCode();
                hash = hash * n + IconColour.GetHashCode();
                hash = hash * n + IconScale.GetHashCode();
                hash = hash * n + LineColour.GetHashCode();
                hash = hash * n + LineWidth.GetHashCode();
                hash = hash * n + PolygonColour.GetHashCode();

                return hash;
            }
        }
    }
}

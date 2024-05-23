namespace Figma
{
    [System.Serializable]
    public class FigmaFile
    {
        public Document document;
    }

    [System.Serializable]
    public class Document
    {
        public Layer[] children;
    }

    [System.Serializable]
    public class Layer
    {
        public string id;
        public string name;
        public string type;
        public Layer[] children;
        public AbsoluteBoundingBox absoluteBoundingBox;
        public Fill[] fills;
        public string characters;
        public Style style;
    }

    [System.Serializable]
    public class AbsoluteBoundingBox
    {
        public float x;
        public float y;
        public float width;
        public float height;
    }

    [System.Serializable]
    public class Fill
    {
        public Color color;
        public string imageRef;

        [System.Serializable]
        public class Color
        {
            public float r;
            public float g;
            public float b;
            public float a;
        }
    }

    [System.Serializable]
    public class Style
    {
        public float fontSize;
    }
}

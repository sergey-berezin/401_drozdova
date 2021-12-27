using System.Drawing;
using System.IO;
using System.Numerics;
using ParallelTask;


namespace WpfApp
{
    public class ImageProxy
    {
        public string Name { get; }
        public Bitmap ImageData { get; set; }
        public int Hash { get; }

        public ImageProxy(string name, string image_path)
        {
            Name = name;
            Hash = new BigInteger(File.ReadAllBytes(image_path)).GetHashCode();
            ImageData = new Bitmap(Image.FromFile(image_path));
        }

        public ImageProxy(Database.Image image_db)
        {
            Name = image_db.Name;
            Hash = image_db.Hash;
            ImageData = new Bitmap(ImageOperations.FromByteArray(image_db.Data));
        }
    }

    public static class ImageProxyUpdater
    {
        public static Bitmap CreateImageWithBBox(Bitmap image, YoloV4Result res)
        {
            var new_image = new Bitmap(image);

            using (var g = Graphics.FromImage(new_image))
            {
                var x1 = res.BBox[0];
                var y1 = res.BBox[1];
                var x2 = res.BBox[2];
                var y2 = res.BBox[3];

                g.DrawRectangle(Pens.Red, x1, y1, x2 - x1, y2 - y1);
                using (var brushes = new SolidBrush(Color.FromArgb(50, Color.Red)))
                {
                    g.FillRectangle(brushes, x1, y1, x2 - x1, y2 - y1);
                }

                g.DrawString(res.Label + " " + res.Confidence.ToString("0.00"),
                    new Font("Arial", 12), Brushes.Blue, new PointF(x1, y1));
            }

            return new_image;
        }
    }

}

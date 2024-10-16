﻿using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net.Sockets;
using System.Text;
using K4AdotNet;
using K4AdotNet.BodyTracking;
using K4AdotNet.Record;
using Microsoft.Azure.Kinect.BodyTracking;
using Microsoft.Azure.Kinect.Sensor;
using Microsoft.Win32;

string basePath = @"C:\Users\flori\OneDrive\Bureau\Kinect_folder";

var mkvPath = $@"C:\Users\flori\OneDrive\Bureau\CHU_St_Justine\005-Device-0-04-05-2024-15-05-41.mkv";

List<PointF> points = new List<PointF>(); // points pour tracer le polygone mais remis à zéro à chaque frame donc nouvelle liste pour écrire le fichier CSV
List<PointF> pointsRGB = new List<PointF>();
List<string> skeletonPositionData = new List<string>();
int countMaskPixels = 0;
List<int> pixelsIndexMask = new List<int>();
double area = 0;
List<double> volumeList = new List<double>();
List<double> meanDepthList = new List<double>();
List<PointF> pointsCSV = new List<PointF>();
string meanFilePath = $@"{basePath}\depth_means.csv";

string server = "127.0.0.1";  // Adresse IP de ton script Python
int port = 5000;  // Port utilisé par le serveur socket côté Python
bool connected = false;

// Chemin vers l'exécutable Python
string pythonExePath = "python"; // fonctionne seulement si python est dans le path
string scriptPath = $@"C:\Users\flori\source\repos\Kinect_V1\Kinect_V1\Python_scripts\socket_plot.py";

//demander l'endroit où le fichier Python est sauvegardé
Console.WriteLine("Entrez le chemin où le fichier Python est sauvegardé :");
string userPathPython = Console.ReadLine();
if (userPathPython != "")
{
    scriptPath = userPathPython;
}

// Créer un nouvel objet Process
ProcessStartInfo start = new ProcessStartInfo();
start.FileName = pythonExePath;
start.Arguments = scriptPath;
start.UseShellExecute = false;
start.RedirectStandardOutput = false;
start.RedirectStandardInput = false;
start.CreateNoWindow = false;

// Démarrer le processus
Process process = new Process();
process.StartInfo = start;
process.Start();


while (!connected)
{
    try
    {
        using (TcpClient client = new TcpClient(server, port))
        using (NetworkStream stream = client.GetStream())
        {
            connected = true;

            // Choix de l'action à réaliser
            Console.WriteLine("Choisissez une action :");
            Console.WriteLine("1. Lire un fichier MKV (Playback)");
            Console.WriteLine("2. Faire un enregistrement en direct");
            string choix = Console.ReadLine();
            //demander l'endroit où sauvegarder les fichiers
            Console.WriteLine("Entrez le chemin où sauvegarder les fichiers :");
            string userPath = Console.ReadLine();
            if (userPath != "")
            {
                basePath = userPath;
            }

            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }

            if (choix == "1")
            {
                //demander le chemin du fichier MKV
                Console.WriteLine("Entrez le chemin du fichier MKV :");
                string mkvuserPath = Console.ReadLine();
                if (mkvuserPath != "")
                {
                    mkvPath = mkvuserPath;
                }
                // Playback mode
                using (var playback = new Playback(mkvPath))
                {
                    // Déclaration de la variable pour stocker la configuration
                    RecordConfiguration recordConfig;

                    // Récupérer la configuration d'enregistrement
                    playback.GetRecordConfiguration(out recordConfig);

                    //get sensor.calibration
                    K4AdotNet.Sensor.Calibration deviceCalibration;
                    playback.GetCalibration(out deviceCalibration);

                    // Utiliser la configuration (afficher certaines informations par exemple)
                    Console.WriteLine($"Depth Mode: {recordConfig.DepthMode}");
                    Console.WriteLine($"Color Resolution: {recordConfig.ColorResolution}");
                    Console.WriteLine($"Color Format: {recordConfig.ColorFormat}");


                    // Déclaration de la variable pour stocker la capture
                    K4AdotNet.Sensor.Capture sensorCapture;

                    playback.SeekTimestamp(Microseconds64.FromSeconds(19), PlaybackSeekOrigin.Begin);

                    using (K4AdotNet.BodyTracking.Tracker tracker = new(deviceCalibration, new K4AdotNet.BodyTracking.TrackerConfiguration() { ProcessingMode = K4AdotNet.BodyTracking.TrackerProcessingMode.Gpu, SensorOrientation = K4AdotNet.BodyTracking.SensorOrientation.Default }))

                    {
                        int imageCount = 0;
                        while (playback.TryGetNextCapture(out sensorCapture))
                        {
                            tracker.EnqueueCapture(sensorCapture);
                            int height = deviceCalibration.DepthCameraCalibration.ResolutionHeight;
                            int width = deviceCalibration.DepthCameraCalibration.ResolutionWidth;

                            // Process the capture
                            using (BodyFrame frame = tracker.PopResult())
                            {
                                if (frame != null)
                                {
                                    if (frame.BodyCount > 0)
                                    {
                                        Console.ForegroundColor = ConsoleColor.Green;
                                        Console.WriteLine("Body detected");
                                        imageCount++;
                                        points.Clear();

                                        //get body skeleton
                                        K4AdotNet.BodyTracking.Skeleton skeleton;
                                        frame.GetBodySkeleton(0, out skeleton);

                                        //Array of joint to track
                                        K4AdotNet.BodyTracking.JointType[] selectedJoints =
                                        {
                                JointType.ShoulderLeft,
                                JointType.Neck,
                                JointType.ShoulderRight,
                                JointType.HipRight,
                                JointType.HipLeft,
                            };
                                        float[] posData = new float[selectedJoints.Length * 3];

                                        for (int i = 0; i < selectedJoints.Length; i++)
                                        {
                                            var joint = skeleton[selectedJoints[i]];
                                            posData[i * 3] = joint.PositionMm.X;
                                            posData[i * 3 + 1] = joint.PositionMm.Y;
                                            posData[i * 3 + 2] = joint.PositionMm.Z;

                                            //Transform the 3D joint position to 2D pixel coordinates using the depth camera
                                            var joint2D = deviceCalibration.Convert3DTo2D(joint.PositionMm, K4AdotNet.Sensor.CalibrationGeometry.Depth, K4AdotNet.Sensor.CalibrationGeometry.Depth);
                                            var jointRGB = deviceCalibration.Convert3DTo2D(joint.PositionMm, K4AdotNet.Sensor.CalibrationGeometry.Depth, K4AdotNet.Sensor.CalibrationGeometry.Color);
                                            points.Add(new PointF(joint2D.Value.X, joint2D.Value.Y));
                                            pointsRGB.Add(new PointF(jointRGB.Value.X, jointRGB.Value.Y));
                                        }

                                        //string currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                        string currentTime = frame.DeviceTimestamp.ToString();

                                        // Add the timestamp and position data to the list
                                        skeletonPositionData.Add($"{currentTime};{string.Join(";", posData)}");


                                        // Get the depth image from the body frame
                                        var depthImage = frame.Capture.DepthImage;

                                        // récupérer les données de profondeur
                                        short[] depthData = new short[height * width];
                                        depthImage.CopyTo(dst: depthData);

                                        if (imageCount > 59)
                                        {
                                            if (imageCount == 60)
                                            {
                                                //Calcule l'aide du masque qu'une seule fois
                                                float scaleFactor = 1.0f; // Agrandir de 20%
                                                points = ScalePolygon(points, scaleFactor);
                                                pointsRGB = ScalePolygon(pointsRGB, scaleFactor);


                                                // Créer un masque pour le polygone
                                                Bitmap mask = new Bitmap(width, height);
                                                using (Graphics g = Graphics.FromImage(mask))
                                                {
                                                    g.Clear(Color.Black);  // Remplir l'image de noir (0)
                                                    Brush brush = new SolidBrush(Color.White); // Brosse blanche pour remplir le polygone (1)

                                                    if (points.Count > 0)
                                                    {
                                                        g.FillPolygon(brush, points.ToArray()); // Dessiner le polygone
                                                    }
                                                }
                                                mask.Save($@"{basePath}\mask_{imageCount}.png");


                                                // Créer un masque pour le polygone
                                                //Get the RGB capture
                                                var colorImage = frame.Capture.ColorImage;

                                                // Get the color data
                                                byte[] colorData = new byte[colorImage.SizeBytes];
                                                colorImage.CopyTo(dst: colorData);

                                                string rgbname = $@"{basePath}\rgb.jpg";
                                                long imageBufferSize = colorImage.SizeBytes;

                                                using (FileStream fileObject = new FileStream(rgbname, FileMode.Create, FileAccess.Write))
                                                {
                                                    fileObject.Write(colorData, 0, (int)imageBufferSize);
                                                }

                                                // Charger l'image RGB après l'enregistrement
                                                Bitmap rgbImage = new(rgbname);

                                                // Créer un masque pour le polygone en utilisant l'image RGB comme fond
                                                using (Graphics g = Graphics.FromImage(rgbImage))
                                                {

                                                    Brush brush = new SolidBrush(Color.White); // Brosse blanche pour remplir le polygone (1)

                                                    if (pointsRGB.Count > 0)
                                                    {
                                                        g.FillPolygon(brush, pointsRGB.ToArray()); // Dessiner le polygone
                                                    }
                                                }

                                                // Sauvegarder l'image avec le polygone sur le fond RGB
                                                rgbImage.Save($@"{basePath}\mask_RGB{imageCount}.png");


                                                string maskPath = $@"{basePath}\mask_60.png";
                                                Bitmap maskRead = new Bitmap(maskPath);
                                                // Parcourir chaque pixel du masque pour sauvegarder les coordonnées des pixels dans une liste
                                                for (int y = 0; y < maskRead.Height; y++)
                                                {
                                                    for (int x = 0; x < maskRead.Width; x++)
                                                    {
                                                        Color pixelColor = maskRead.GetPixel(x, y);

                                                        // Si le pixel est blanc (appartenant au masque), traiter ses valeurs
                                                        if (pixelColor.R == 255 && pixelColor.G == 255 && pixelColor.B == 255) // Blanc
                                                        {
                                                            int pixelIndex = y * maskRead.Width + x; // Index du pixel dans le tableau de profondeur
                                                            pixelsIndexMask.Add(pixelIndex);

                                                            // Ajouter la valeur de profondeur correspondante à la somme
                                                            countMaskPixels += 1;
                                                        }
                                                    }
                                                }

                                                // store the body index map
                                                string bodyIndexMapPath = $@"{basePath}\bodyIndexMap_{imageCount}.png";
                                                var bodyIndexMap = frame.BodyIndexMap;
                                                byte[] bodyIndexData = new byte[bodyIndexMap.SizeBytes];
                                                bodyIndexMap.CopyTo(dst: bodyIndexData);
                                                long bodyIndexBufferSize = bodyIndexMap.SizeBytes;

                                                // Get the width and height of the body index map
                                                int width_2 = bodyIndexMap.WidthPixels;
                                                int height_2 = bodyIndexMap.HeightPixels;

                                                // Create a bitmap to store the image
                                                using (Bitmap bitmap = new Bitmap(width_2, height_2, PixelFormat.Format8bppIndexed))
                                                {
                                                    // Lock the bits of the image to access the pixel data
                                                    BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, bitmap.PixelFormat);

                                                    // Copy the body index data into the bitmap's buffer
                                                    System.Runtime.InteropServices.Marshal.Copy(bodyIndexData, 0, bmpData.Scan0, (int)bodyIndexBufferSize);

                                                    // Unlock the bits of the image
                                                    bitmap.UnlockBits(bmpData);

                                                    // Save the bitmap as a PNG file
                                                    bitmap.Save(bodyIndexMapPath);
                                                }

                                                // Exemple pour une seule entrée de skeletonPositionData
                                                string[] data = skeletonPositionData[59].Split(';'); // On utilise la première entrée (index 0)

                                                // Extraire les coordonnées 2D des articulations nécessaires
                                                PointF shoulderLeft = new PointF(float.Parse(data[1]), float.Parse(data[2]));  // X, Y de ShoulderLeft
                                                PointF neck = new PointF(float.Parse(data[4]), float.Parse(data[5]));        // X, Y de Neck
                                                PointF shoulderRight = new PointF(float.Parse(data[7]), float.Parse(data[8])); // X, Y de ShoulderRight
                                                PointF hipRight = new PointF(float.Parse(data[10]), float.Parse(data[11]));      // X, Y de HipRight
                                                PointF hipLeft = new PointF(float.Parse(data[13]), float.Parse(data[14]));     // X, Y de HipLeft

                                                // Liste des points formant le polygone
                                                List<PointF> polygonPoints = new List<PointF> { shoulderLeft, neck, shoulderRight, hipRight, hipLeft };

                                                // Calculer l'aire du polygone
                                                area = CalculatePolygonArea(polygonPoints);

                                            }
                                            // Récupérer les données de profondeur associées
                                            double sumDepthValues = 0;
                                            for (int i = 0; i < pixelsIndexMask.Count; i++)
                                            {
                                                sumDepthValues += depthData[pixelsIndexMask[i]];
                                            }
                                            // Calculer la moyenne des valeurs de profondeur dans le masque
                                            double meanDepthValue = (countMaskPixels > 0) ? sumDepthValues / countMaskPixels : 0;
                                            meanDepthList.Add(meanDepthValue);
                                            double volume = area * meanDepthValue;
                                            volumeList.Add(volume);
                                            Console.WriteLine($"Volume pour la frame {imageCount}: {volume / 1000} mL");

                                            // Formater le nombre avec 3 chiffres avant la virgule, 1 virgule, et 13 chiffres après la virgule. Modifier le client en fonction de ce format.
                                            double volumeInmL = volume / 1000;
                                            string formattedNumber = volumeInmL.ToString("000000.000000000");
                                            byte[] message = Encoding.ASCII.GetBytes(formattedNumber + "\n");
                                            // Envoi de la donnée au script Python
                                            stream.Write(message);
                                        }
                                        else
                                        {
                                            Console.ForegroundColor = ConsoleColor.Red;
                                            Console.Write("No Person");

                                        }
                                        Console.ResetColor();
                                    }
                                }
                            }
                        }
                        Console.WriteLine($"\rStop Recording. {imageCount} images captured.");
                        // Envoyer une chaîne spéciale pour indiquer la fin de la transmission
                        byte[] endMessage = Encoding.ASCII.GetBytes("END\n");
                        stream.Write(endMessage, 0, endMessage.Length);

                        stream.Close();
                        client.Close();
                    }
                }
            }

            else if (choix == "2")
            {
                Console.WriteLine("Démarrage de l'enregistrement en direct...");
                using (var device = K4AdotNet.Sensor.Device.Open(0))
                {
                    // Configure the device
                    device.StartCameras(new K4AdotNet.Sensor.DeviceConfiguration()
                    {
                        ColorFormat = K4AdotNet.Sensor.ImageFormat.ColorBgra32,
                        ColorResolution = K4AdotNet.Sensor.ColorResolution.R720p,
                        DepthMode = K4AdotNet.Sensor.DepthMode.NarrowView2x2Binned,
                        SynchronizedImagesOnly = true,
                        WiredSyncMode = K4AdotNet.Sensor.WiredSyncMode.Standalone,
                        CameraFps = K4AdotNet.Sensor.FrameRate.Thirty,
                    });

                    //Camera calibration
                    K4AdotNet.Sensor.Calibration deviceCalibration;
                    device.GetCalibration(K4AdotNet.Sensor.DepthMode.NarrowView2x2Binned, K4AdotNet.Sensor.ColorResolution.R720p, out deviceCalibration);

                    using (K4AdotNet.BodyTracking.Tracker tracker = new(deviceCalibration, new K4AdotNet.BodyTracking.TrackerConfiguration() { ProcessingMode = K4AdotNet.BodyTracking.TrackerProcessingMode.Gpu, SensorOrientation = K4AdotNet.BodyTracking.SensorOrientation.Default }))

                    {
                        // Démarrer le chronomètre
                        Stopwatch stopwatch = new Stopwatch();
                        stopwatch.Start();
                        int imageCount = 0;
                        int height = deviceCalibration.DepthCameraCalibration.ResolutionHeight;
                        int width = deviceCalibration.DepthCameraCalibration.ResolutionWidth;


                        while (stopwatch.Elapsed < TimeSpan.FromSeconds(32)) // La boucle tourne pendant 32 secondes
                        {
                            using (K4AdotNet.Sensor.Capture sensorCapture = device.GetCapture())
                            {
                                tracker.EnqueueCapture(sensorCapture);
                            }


                            // Process the capture
                            using (BodyFrame frame = tracker.PopResult())
                            {
                                if (frame != null)
                                {
                                    if (frame.BodyCount > 0)
                                    {
                                        Console.ForegroundColor = ConsoleColor.Green;
                                        Console.WriteLine("Body detected");
                                        imageCount++;
                                        points.Clear();

                                        //get body skeleton
                                        K4AdotNet.BodyTracking.Skeleton skeleton;
                                        frame.GetBodySkeleton(0, out skeleton);

                                        //Array of joint to track
                                        K4AdotNet.BodyTracking.JointType[] selectedJoints =
                                        {
                                JointType.ShoulderLeft,
                                JointType.Neck,
                                JointType.ShoulderRight,
                                JointType.HipRight,
                                JointType.HipLeft,
                            };
                                        float[] posData = new float[selectedJoints.Length * 3];

                                        for (int i = 0; i < selectedJoints.Length; i++)
                                        {
                                            var joint = skeleton[selectedJoints[i]];
                                            posData[i * 3] = joint.PositionMm.X;
                                            posData[i * 3 + 1] = joint.PositionMm.Y;
                                            posData[i * 3 + 2] = joint.PositionMm.Z;

                                            //Transform the 3D joint position to 2D pixel coordinates using the depth camera
                                            var joint2D = deviceCalibration.Convert3DTo2D(joint.PositionMm, K4AdotNet.Sensor.CalibrationGeometry.Depth, K4AdotNet.Sensor.CalibrationGeometry.Depth);
                                            points.Add(new PointF(joint2D.Value.X, joint2D.Value.Y));
                                        }

                                        //string currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                        string currentTime = frame.DeviceTimestamp.ToString();

                                        // Add the timestamp and position data to the list
                                        skeletonPositionData.Add($"{currentTime};{string.Join(";", posData)}");


                                        // Get the depth image from the body frame
                                        var depthImage = frame.Capture.DepthImage;

                                        // récupérer les données de profondeur
                                        short[] depthData = new short[height * width];
                                        depthImage.CopyTo(dst: depthData);

                                        if (imageCount > 59)
                                        {
                                            if (imageCount == 60)
                                            {
                                                //Calcule l'aide du masque qu'une seule fois
                                                //Calcule l'aide du masque qu'une seule fois
                                                float scaleFactor = 1.0f; // Agrandir de 20%
                                                points = ScalePolygon(points, scaleFactor);
                                                pointsRGB = ScalePolygon(pointsRGB, scaleFactor);

                                                // Créer un masque pour le polygone
                                                Bitmap mask = new Bitmap(width, height);
                                                using (Graphics g = Graphics.FromImage(mask))
                                                {
                                                    g.Clear(Color.Black);  // Remplir l'image de noir (0)
                                                    Brush brush = new SolidBrush(Color.White); // Brosse blanche pour remplir le polygone (1)

                                                    if (points.Count > 0)
                                                    {
                                                        g.FillPolygon(brush, points.ToArray()); // Dessiner le polygone
                                                    }
                                                }
                                                mask.Save($@"{basePath}\mask_{imageCount}.png");


                                                // Créer un masque pour le polygone
                                                //Get the RGB capture
                                                var colorImage = frame.Capture.ColorImage;

                                                // Get the color data
                                                byte[] colorData = new byte[colorImage.SizeBytes];
                                                colorImage.CopyTo(dst: colorData);
                                                string rgbname = $@"{basePath}\rgb.jpg";
                                                long imageBufferSize = colorImage.SizeBytes;

                                                using (FileStream fileObject = new FileStream(rgbname, FileMode.Create, FileAccess.Write))
                                                {
                                                    fileObject.Write(colorData, 0, (int)imageBufferSize);
                                                }

                                                // Charger l'image RGB après l'enregistrement
                                                Bitmap rgbImage = new(rgbname);

                                                // Créer un masque pour le polygone en utilisant l'image RGB comme fond
                                                using (Graphics g = Graphics.FromImage(rgbImage))
                                                {

                                                    Brush brush = new SolidBrush(Color.White); // Brosse blanche pour remplir le polygone (1)

                                                    if (pointsRGB.Count > 0)
                                                    {
                                                        g.FillPolygon(brush, pointsRGB.ToArray()); // Dessiner le polygone
                                                    }
                                                }

                                                // Sauvegarder l'image avec le polygone sur le fond RGB
                                                rgbImage.Save($@"{basePath}\mask_RGB{imageCount}.png");



                                                string maskPath = $@"{basePath}\mask_60.png";
                                                Bitmap maskRead = new Bitmap(maskPath);
                                                // Parcourir chaque pixel du masque pour sauvegarder les coordonnées des pixels dans une liste
                                                for (int y = 0; y < maskRead.Height; y++)
                                                {
                                                    for (int x = 0; x < maskRead.Width; x++)
                                                    {
                                                        Color pixelColor = maskRead.GetPixel(x, y);

                                                        // Si le pixel est blanc (appartenant au masque), traiter ses valeurs
                                                        if (pixelColor.R == 255 && pixelColor.G == 255 && pixelColor.B == 255) // Blanc
                                                        {
                                                            int pixelIndex = y * maskRead.Width + x; // Index du pixel dans le tableau de profondeur
                                                            pixelsIndexMask.Add(pixelIndex);

                                                            // Ajouter la valeur de profondeur correspondante à la somme
                                                            countMaskPixels += 1;
                                                        }
                                                    }
                                                }
                                                // Exemple pour une seule entrée de skeletonPositionData
                                                string[] data = skeletonPositionData[59].Split(';'); // On utilise la première entrée (index 0)

                                                // Extraire les coordonnées 2D des articulations nécessaires
                                                PointF shoulderLeft = new PointF(float.Parse(data[1]), float.Parse(data[2]));  // X, Y de ShoulderLeft
                                                PointF neck = new PointF(float.Parse(data[4]), float.Parse(data[5]));        // X, Y de Neck
                                                PointF shoulderRight = new PointF(float.Parse(data[7]), float.Parse(data[8])); // X, Y de ShoulderRight
                                                PointF hipRight = new PointF(float.Parse(data[10]), float.Parse(data[11]));      // X, Y de HipRight
                                                PointF hipLeft = new PointF(float.Parse(data[13]), float.Parse(data[14]));     // X, Y de HipLeft

                                                // Liste des points formant le polygone
                                                List<PointF> polygonPoints = new List<PointF> { shoulderLeft, neck, shoulderRight, hipRight, hipLeft };

                                                // Calculer l'aire du polygone
                                                area = CalculatePolygonArea(polygonPoints);

                                            }
                                            // Récupérer les données de profondeur associées
                                            double sumDepthValues = 0;
                                            for (int i = 0; i < pixelsIndexMask.Count; i++)
                                            {
                                                sumDepthValues += depthData[pixelsIndexMask[i]];
                                            }
                                            // Calculer la moyenne des valeurs de profondeur dans le masque
                                            double meanDepthValue = (countMaskPixels > 0) ? sumDepthValues / countMaskPixels : 0;
                                            meanDepthList.Add(meanDepthValue);
                                            double volume = area * meanDepthValue;
                                            volumeList.Add(volume);
                                            Console.WriteLine($"Volume pour la frame {imageCount}: {volume / 1000} mL");

                                            // Formater le nombre avec 3 chiffres avant la virgule, 1 virgule, et 13 chiffres après la virgule. Modifier le client en fonction de ce format.
                                            double volumeInmL = volume / 1000;
                                            string formattedNumber = volumeInmL.ToString("000000.000000000");
                                            byte[] message = Encoding.ASCII.GetBytes(formattedNumber + "\n");
                                            // Envoi de la donnée au script Python
                                            stream.Write(message);
                                        }
                                        else
                                        {
                                            Console.ForegroundColor = ConsoleColor.Red;
                                            Console.Write("No Person");

                                        }
                                        Console.ResetColor();
                                    }
                                }
                            }
                            Console.WriteLine($"\rStop Recording. {imageCount} images captured.");
                            // Envoyer une chaîne spéciale pour indiquer la fin de la transmission
                            byte[] endMessage = Encoding.ASCII.GetBytes("END\n");
                            stream.Write(endMessage, 0, endMessage.Length);

                            stream.Close();
                            client.Close();
                        }
                    }

                }
            }

        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to connect to the server: {ex.Message}");
        Console.WriteLine("Retrying in 5 seconds...");
        System.Threading.Thread.Sleep(5000);
    }
}

// create a pos data file in the current directory
string fileName = $@"{basePath}\{DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss")}_posdata.csv";
// create a header for the CSV file
string header = "Timestamp;ShoulderLeft_X;ShoulderLeft_Y;ShoulderLeft_Z;Neck_X;Neck_Y;Neck_Z;ShoulderRight_X;ShoulderRight_Y;ShoulderRight_Z;HipRight_X;HipRight_Y;HipRight_Z;HipLeft_X;HipLeft_Y;HipLeft_Z";


// Write the captured position data to the CSV file using the header
File.WriteAllText(fileName, header + Environment.NewLine);
File.AppendAllText(fileName, string.Join(Environment.NewLine, skeletonPositionData));
Console.WriteLine($"3D coordinates of joints have been saved in {fileName}");

// Write 2D joint coordinates to a new CSV file
string fileName2D = $@"{basePath}\{DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss")}_posdata2D.csv";
string header2D = "ShoulderLeft_X;ShoulderLeft_Y;Neck_X;Neck_Y;ShoulderRight_X;ShoulderRight_Y;HipRight_X;HipRight_Y;HipLeft_X;HipLeft_Y";
File.WriteAllText(fileName2D, header2D + Environment.NewLine);
using (StreamWriter sw2D = new StreamWriter(fileName2D, true))
{
    for (int i = 0; i < pointsCSV.Count; i += 5)
    {
        sw2D.WriteLine($"{pointsCSV[i].X};{pointsCSV[i].Y};{pointsCSV[i + 1].X};{pointsCSV[i + 1].Y};{pointsCSV[i + 2].X};{pointsCSV[i + 2].Y};{pointsCSV[i + 3].X};{pointsCSV[i + 3].Y};{pointsCSV[i + 4].X};{pointsCSV[i + 4].Y}");
    }
    Console.WriteLine($"2D coordinates of joints have been saved in {fileName2D}");

}

// Ajouter un en-tête au fichier CSV
File.WriteAllText(meanFilePath, "MaskIndex;MeanDepthValue;PixelCount;Area;Volume;Volume en mL" + Environment.NewLine);

// Boucle pour traiter chaque masque et ses données de profondeur
for (int i = 0; i < meanDepthList.Count; i++)
{
    double meanDepthValue = meanDepthList[i];
    double volume = volumeList[i];
    File.AppendAllText(meanFilePath, $"{i + 1};{meanDepthValue};{countMaskPixels};{area};{volume};{volume / 1000}" + Environment.NewLine);
    //Console.WriteLine($"Moyenne des valeurs de profondeur pour le masque {i + 1} ajoutée au fichier.");
}

Console.WriteLine($"Toutes les moyennes des masques ont été sauvegardées dans {meanFilePath}");
Console.Write("\r" + new string(' ', Console.WindowWidth));
Console.WriteLine("\rStop Recording.");



static double CalculatePolygonArea(List<PointF> points)
{
    int n = points.Count;
    double area = 0;

    for (int i = 0; i < n - 1; i++)
    {
        area += points[i].X * points[i + 1].Y - points[i + 1].X * points[i].Y;
    }

    // Add the last term
    area += points[n - 1].X * points[0].Y - points[0].X * points[n - 1].Y;

    return Math.Abs(area) / 2;
}

static List<PointF> ScalePolygon(List<PointF> points, float scaleFactor)
{
    // Calculer le centre du polygone
    float centerX = 0;
    float centerY = 0;
    foreach (var point in points)
    {
        centerX += point.X;
        centerY += point.Y;
    }
    centerX /= points.Count;
    centerY /= points.Count;

    // Appliquer la mise à l'échelle
    List<PointF> scaledPoints = new List<PointF>();
    foreach (var point in points)
    {
        float scaledX = centerX + (point.X - centerX) * scaleFactor;
        float scaledY = centerY + (point.Y - centerY) * scaleFactor;
        scaledPoints.Add(new PointF(scaledX, scaledY));
    }

    return scaledPoints;
}
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Azure.Kinect.BodyTracking;
using Microsoft.Azure.Kinect.Sensor;

// Open device.
using (Device device = Device.Open())
{
    device.StartCameras(new DeviceConfiguration()
    {
        ColorFormat = ImageFormat.ColorBGRA32,
        ColorResolution = ColorResolution.R720p,
        DepthMode = DepthMode.NFOV_2x2Binned,
        SynchronizedImagesOnly = true,
        WiredSyncMode = WiredSyncMode.Standalone,
        CameraFPS = FPS.FPS30
    });

    // Camera calibration.
    var deviceCalibration = device.GetCalibration();
    var transformation = deviceCalibration.CreateTransformation();

    // list to store the position data 3D
    List<string> skeletonPositionData = new List<string>();
    List<PointF> points = new List<PointF>();
    List<ushort[]> depthDataList = new List<ushort[]>();


    using (Tracker tracker = Tracker.Create(deviceCalibration, new TrackerConfiguration() { ProcessingMode = TrackerProcessingMode.Gpu, SensorOrientation = SensorOrientation.Default }))
    {
        var isActive = true;
        int imageCount = 0;
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            isActive = false;
        };
        Console.WriteLine("Start Recording. Press Ctrl+C to stop.");

        while (isActive)
        {
            using (Capture sensorCapture = device.GetCapture())
            {
                // Queue latest frame from the sensor.
                tracker.EnqueueCapture(sensorCapture);
                //imageCount++;
            }

            // Try getting the latest tracker frame.
            using (Frame frame = tracker.PopResult(TimeSpan.FromMilliseconds(400), throwOnTimeout: true))
            {
                if (frame != null)
                {
                    // Is there a person?
                    if (frame.NumberOfBodies > 0)
                    {
                        Console.Write("\r" + new string(' ', Console.WindowWidth));
                        Console.Write("\rIs there a person: ");
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write("Yes");
                        imageCount++;


                        int height = deviceCalibration.DepthCameraCalibration.ResolutionHeight;
                        int width = deviceCalibration.DepthCameraCalibration.ResolutionWidth;

                        // get body skeleton
                        var skeleton = frame.GetBodySkeleton(0);

                        // joints to track
                        JointId[] selectedJoints =
                        {
                            JointId.ShoulderLeft,
                            JointId.ShoulderRight,
                            JointId.HipRight,
                            JointId.HipLeft,
                        };

                        float[] posData = new float[selectedJoints.Length * 3];

                        for (int i = 0; i < selectedJoints.Length; i++)
                        {
                            var joint = skeleton.GetJoint(selectedJoints[i]);

                            // store X, Y, Z coordinates in posData array
                            posData[i * 3] = joint.Position.X;
                            posData[i * 3 + 1] = joint.Position.Y;
                            posData[i * 3 + 2] = joint.Position.Z;

                            //Transform the 3D joint position to 2D pixel coordinates using the depth camera
                            var joint2D = deviceCalibration.TransformTo2D(joint.Position, CalibrationDeviceType.Depth, CalibrationDeviceType.Depth);
                            points.Add(new PointF(joint2D.Value.X, joint2D.Value.Y));

                        }

                        //string currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                        string currentTime = frame.Capture.Depth.DeviceTimestamp.ToString();

                        // Add the timestamp and position data to the list
                        skeletonPositionData.Add($"{currentTime};{string.Join(";", posData)}");


                        // Get the depth image
                        Microsoft.Azure.Kinect.Sensor.Image depthImage = frame.Capture.Depth;

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

                        // Sauvegarder une image pour chaque frame dans un dossier bin
                        mask.Save($@"C:\Users\flori\OneDrive\Bureau\Kinect_folder\mask_{imageCount}.png");

                        // Accéder aux données brutes de l'image de profondeur
                        ushort[] depthData = depthImage.GetPixels<ushort>().ToArray(); // Obtenir les données de profondeur
                        depthDataList.Add(depthData);

                    }
                    else
                    {
                        Console.Write("\r" + new string(' ', Console.WindowWidth));
                        Console.Write("\rIs there a person: ");
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write("No Person");
                    }

                    Console.ResetColor();
                }
            }
        }
        Console.WriteLine($"\rStop Recording. {imageCount} images captured.");

        // get current directory
        string currentDirectory = "C:\\Users\\flori\\OneDrive\\Bureau\\Kinect_folder";
        // create a pos data file in the current directory
        string fileName = $@"{currentDirectory}\{DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss")}_posdata.csv";
        // create a header for the CSV file
        string header = "Timestamp;ShoulderLeft_X;ShoulderLeft_Y;ShoulderLeft_Z;ShoulderRight_X;ShoulderRight_Y;ShoulderRight_Z;HipRight_X;HipRight_Y;HipRight_Z;HipLeft_X;HipLeft_Y;HipLeft_Z";


        // Write the captured position data to the CSV file using the header
        File.WriteAllText(fileName, header + Environment.NewLine);
        File.AppendAllText(fileName, string.Join(Environment.NewLine, skeletonPositionData));
        Console.WriteLine($"3D coordinates of joints have been saved in {fileName}");

        // Write 2D joint coordinates to a new CSV file
        string fileName2D = $@"{currentDirectory}\{DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss")}_posdata2D.csv";
        string header2D = "ShoulderLeft_X;ShoulderLeft_Y;ShoulderRight_X;ShoulderRight_Y;HipRight_X;HipRight_Y;HipLeft_X;HipLeft_Y";
        File.WriteAllText(fileName2D, header2D + Environment.NewLine);
        using (StreamWriter sw2D = new StreamWriter(fileName2D, true))
        {
            for (int i = 0; i < points.Count; i += 4)
            {
                sw2D.WriteLine($"{points[i].X};{points[i].Y};{points[i + 1].X};{points[i + 1].Y};{points[i + 2].X};{points[i + 2].Y};{points[i + 3].X};{points[i + 3].Y}");
            }
            Console.WriteLine($"2D coordinates of joints have been saved in {fileName2D}");

        }

        // Créer un fichier CSV pour stocker les moyennes des valeurs de profondeur pour chaque masque
        string meanFilePath = $@"C:\Users\flori\OneDrive\Bureau\Kinect_folder\depth_means.csv";

        // Ajouter un en-tête au fichier CSV
        File.WriteAllText(meanFilePath, "MaskIndex;MeanDepthValue;PixelCount" + Environment.NewLine);

        // Boucle pour traiter chaque masque et ses données de profondeur
        for (int i = 0; i < imageCount; i++)
        {
            // Charger l'image du masque
            string maskPath = $@"C:\Users\flori\OneDrive\Bureau\Kinect_folder\mask_{i + 1}.png";
            Bitmap mask = new Bitmap(maskPath);

            // Récupérer les données de profondeur associées
            ushort[] depthData = depthDataList[i]; // Les données de profondeur pour l'image actuelle

            // Initialiser les variables pour la somme des pixels et la somme des valeurs de profondeur
            int sumMaskPixels = 0;
            double sumDepthValues = 0;
            int countMaskPixels = 0;

            // Parcourir chaque pixel du masque
            for (int y = 0; y < mask.Height; y++)
            {
                for (int x = 0; x < mask.Width; x++)
                {
                    Color pixelColor = mask.GetPixel(x, y);

                    // Si le pixel est blanc (appartenant au masque), traiter ses valeurs
                    if (pixelColor.R == 255 && pixelColor.G == 255 && pixelColor.B == 255) // Blanc
                    {
                        int pixelIndex = y * mask.Width + x; // Index du pixel dans le tableau de profondeur

                        // Ajouter la valeur de profondeur correspondante à la somme
                        sumDepthValues += depthData[pixelIndex];

                        // Ajouter un à la somme des pixels du masque
                        sumMaskPixels += 1;
                        countMaskPixels++;
                    }
                }
            }

            // Calculer la moyenne des valeurs de profondeur dans le masque
            double meanDepthValue = (countMaskPixels > 0) ? sumDepthValues / countMaskPixels : 0;

            // Écrire la moyenne dans le fichier CSV
            File.AppendAllText(meanFilePath, $"{i + 1};{meanDepthValue};{countMaskPixels}" + Environment.NewLine);
            //Console.WriteLine($"Moyenne des valeurs de profondeur pour le masque {i + 1} ajoutée au fichier.");
        }

        Console.WriteLine($"Toutes les moyennes des masques ont été sauvegardées dans {meanFilePath}");


    }
}
Console.Write("\r" + new string(' ', Console.WindowWidth));
Console.WriteLine("\rStop Recording.");
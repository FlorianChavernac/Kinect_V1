using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Azure.Kinect.BodyTracking;
using Microsoft.Azure.Kinect.Sensor;

//get current directry
//string currentDirectory = Directory.GetCurrentDirectory();
string currentDirectory = "C:\\Users\\flori\\OneDrive\\Bureau\\Kinect_folder";
// create a pos data file in the current directry.
string fileName = $@"{currentDirectory}\{DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss")}_posdata.csv";
//Create a header for the CSV file
string header = "Timestamp;ShoulderLeft_X;ShoulderLeft_Y;ShoulderLeft_Z;ShoulderRight_X;ShoulderRight_Y;ShoulderRight_Z;HipRight_X;HipRight_Y;HipRight_Z;HipLeft_X;HipLeft_Y;HipLeft_Z";
//Write the header to the CSV file
File.WriteAllText(fileName, header + Environment.NewLine);

// capture and write pos data
using (StreamWriter sw = new StreamWriter(fileName, true))
{
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

        List<PointF> points = [];



        using (Tracker tracker = Tracker.Create(deviceCalibration, new TrackerConfiguration() { ProcessingMode = TrackerProcessingMode.Gpu, SensorOrientation = SensorOrientation.Default }))
        {
            var isActive = true;
            int imageCount = 0; // Initialiser le compteur d'images
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
                    imageCount++;
                }

                // Try getting latest tracker frame.
                using (Frame frame = tracker.PopResult(TimeSpan.FromMilliseconds(400), throwOnTimeout: true))
                {
                    if (frame != null)
                    {
                        // is Human?
                        if (frame.NumberOfBodies > 0)
                        {
                            Console.Write("\r" + new string(' ', Console.WindowWidth));
                            Console.Write("\rIs there person: ");
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.Write("Yes");

                            // Dimensions de l'image (720p) pour afficher le masque dessus
                            int width = 320;
                            int height = 288;

                            // Créer une image vide (masque)
                            Bitmap mask = new(width, height);


                            // get body skeleton
                            var skeleton = frame.GetBodySkeleton(0);

                            // Tableau des articulations à récupérer
                            JointId[] selectedJoints =
                            [
                                JointId.ShoulderLeft,
                                JointId.ShoulderRight,
                                JointId.HipRight,
                                JointId.HipLeft,

                            ];

                            float[] posData = new float[selectedJoints.Length * 3];

                            for (int i = 0; i < selectedJoints.Length; i++)
                            {
                                var joint = skeleton.GetJoint(selectedJoints[i]);

                                // Stocker les coordonnées X, Y, Z dans le tableau posData
                                posData[i * 3] = joint.Position.X;
                                posData[i * 3 + 1] = joint.Position.Y;
                                posData[i * 3 + 2] = joint.Position.Z;

                                var joint2D = deviceCalibration.TransformTo2D(joint.Position, CalibrationDeviceType.Depth, CalibrationDeviceType.Depth);

                                points.Add(new PointF(joint2D.Value.X, joint2D.Value.Y));
                            }


                            string currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

                            // Écrire le temps et les données de position dans le CSV
                            sw.WriteLine($"{currentTime};{string.Join(";", posData)}");

                            //Remplir le polygone dans l'image

                            //using (Graphics g = Graphics.FromImage(mask))
                            //    {
                            //        g.Clear(Color.Black);  // Remplir l'image de noir (0)

                            //        // Définir une brosse blanche pour remplir le polygone (1)
                            //        Brush brush = new SolidBrush(Color.White);
                            //        if (points.Count > 0)
                            //        {
                            //            g.FillPolygon(brush, points.ToArray());
                            //        }
                            //    }

                            //    // Sauvegarder l'image comme fichier PNG (binaire)
                            //    // Créer un nom de fichier unique avec un timestamp
                            //    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                            //    string maskFileName = $@"C:\Users\flori\OneDrive\Bureau\Kinect_folder\masque_{timestamp}.png";

                            //    // Sauvegarder l'image avec le nom de fichier unique
                            //    mask.Save(maskFileName);


                        }
                    }
                    else
                    {
                        Console.Write("\r" + new string(' ', Console.WindowWidth));
                        Console.Write("\rIs there person: ");
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write("No Person");
                    }

                    Console.ResetColor();
                }
            }
            Console.WriteLine($"\rStop Recording. {imageCount} images captured."); // Afficher le nombre total d'images capturées

            // Ecrire le nombre total d'images capturées dans le CSV
            sw.WriteLine($"Total images captured: {imageCount}");


            //Ecire dans un nouveau fichier csv les coordonnées des joints 2D contenues dans la liste points sachant que la structure de la liste est la suivante: [x1, y1, x2, y2, x3, y3, x4, y4] ou 1, 2, 3, 4 correspondent respectivement à ShoulderLeft, ShoulderRight, HipRight, HipLeft
            string fileName2D = $@"{currentDirectory}\{DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss")}_posdata2D.csv";
            string header2D = "Timestamp;ShoulderLeft_X;ShoulderLeft_Y;ShoulderRight_X;ShoulderRight_Y;HipRight_X;HipRight_Y;HipLeft_X;HipLeft_Y";
            File.WriteAllText(fileName2D, header2D + Environment.NewLine);
            using (StreamWriter sw2D = new StreamWriter(fileName2D, true))
            {
                for (int i = 0; i < points.Count; i += 4)
                {
                    string currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    sw2D.WriteLine($"{currentTime};{points[i].X};{points[i].Y};{points[i + 1].X};{points[i + 1].Y};{points[i + 2].X};{points[i + 2].Y};{points[i + 3].X};{points[i + 3].Y}");
                }
            }
            Console.WriteLine(
                $"2D coordinates of joints have been saved in {fileName2D}"); // Afficher le nom du fichier CSV contenant les coordonnées 2D des joints


        }
    }
    Console.Write("\r" + new string(' ', Console.WindowWidth));
    Console.WriteLine("\rStop Recording.");
}
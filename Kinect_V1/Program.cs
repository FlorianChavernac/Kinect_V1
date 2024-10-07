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



        using (Tracker tracker = Tracker.Create(deviceCalibration, new TrackerConfiguration() { ProcessingMode = TrackerProcessingMode.Gpu, SensorOrientation = SensorOrientation.Default }))
        {
            var isActive = true;
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
                }

                // Try getting latest tracker frame.
                using (Frame frame = tracker.PopResult(TimeSpan.Zero, throwOnTimeout: false))
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

                            // Dimensions de l'image (720p)
                            int width = 320;
                            int height = 288;

                            // Créer une image vide (masque)
                            Bitmap mask = new Bitmap(width, height);


                            // get body
                            var skeleton = frame.GetBodySkeleton(0);

                            // Listes pour stocker les valeurs X et Y des articulations
                            List<float> xPositions = new List<float>();
                            List<float> yPositions = new List<float>();

                            //float[] posData = new float[(int)JointId.Count * 3];
                            // Tableau des articulations à récupérer
                            JointId[] selectedJoints = new JointId[]
                            {
                                JointId.ShoulderLeft,
                                JointId.ShoulderRight,
                                JointId.HipRight,
                                JointId.HipLeft,

                            };

                            float[] posData = new float[selectedJoints.Length * 3 + 1];
                            List<PointF> points = new List<PointF>(); // Remplacer Point par PointF

                            // Obtenez l'heure actuelle
                            var currentTime = DateTime.Now; // Vous pouvez utiliser DateTime.UtcNow si vous préférez UTC

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
                            // Obtenez les millisecondes
                            string timestamp = currentTime.ToString("yyyy-MM-dd HH:mm:ss.fff"); // Format avec millisecondes

                            // Écrire le temps et les données de position dans le CSV
                            sw.WriteLine($"{timestamp};{string.Join(";", posData)}");

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
            }
        }
        Console.Write("\r" + new string(' ', Console.WindowWidth));
        Console.WriteLine("\rStop Recording.");
    }
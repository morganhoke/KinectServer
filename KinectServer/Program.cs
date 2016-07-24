using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Kinect;

namespace KinectServer
{
    class Program
    {
        private const string HeadKey = "h";
        private const string RightHandPosKey = "rp";
        private const string LeftHandPosKey = "lp";
        private static KinectSensor sensor;
        private static Socket socket;
        private static IPEndPoint endPoint;

        private static TransformSmoothParameters smoothingParam;

        static void Main(string[] args)
        {
            smoothingParam = new TransformSmoothParameters();
            {
                smoothingParam.Smoothing = 0.7f;
                smoothingParam.Correction = 0.3f;
                smoothingParam.Prediction = 1.0f;
                smoothingParam.JitterRadius = 1.0f;
                smoothingParam.MaxDeviationRadius = 1.0f;
            };

            InitKinect();
            InitNetwork();
            Console.Read();
        }

        private static void Sensor_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            using (SkeletonFrame frame = e.OpenSkeletonFrame())
            {
                if (frame == null)
                {
                    return;
                }
                var skeletons = new Skeleton[frame.SkeletonArrayLength];
                frame.CopySkeletonDataTo(skeletons);

                foreach (var skeleton in skeletons)
                {
                    if (skeleton.TrackingState == SkeletonTrackingState.Tracked)
                    {
                        var head = skeleton.Joints[JointType.Head].Position;
                        var headString = HeadKey + "," + head.X + "," + head.Y + "," + head.Z + "," + DateTime.Now.Ticks + ";";

                        var rightHand = skeleton.Joints[JointType.HandRight].Position;
                        var rhPosString = RightHandPosKey + "," + rightHand.X + "," + rightHand.Y + "," + rightHand.Z + "," + DateTime.Now.Ticks + ";";

                        var leftHand = skeleton.Joints[JointType.HandLeft].Position;
                        var lhPosString = LeftHandPosKey + "," + leftHand.X + "," + leftHand.Y + "," + leftHand.Z + "," + DateTime.Now.Ticks + ";";

                        var output = headString + rhPosString + lhPosString;

                        byte[] bytes = new byte[output.Length * sizeof(char)];
                        socket.SendTo(Encoding.ASCII.GetBytes(output), endPoint);
                    }
                }
            }
        }

        private static void InitKinect()
        {
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    sensor = potentialSensor;
                    break;
                }
            }

            if (sensor != null)
            {
                // Turn on the skeleton stream to receive skeleton frames
                sensor.SkeletonStream.Enable();

                // Add an event handler to be called whenever there is new color frame data
                sensor.SkeletonFrameReady += Sensor_SkeletonFrameReady;

                // Start the sensor!
                try
                {
                    sensor.Start();
                    return;
                }
                catch (IOException)
                {
                    sensor = null;
                }
            }
            Console.WriteLine("ERROR: could not find a kniect.  Be sure you have all drivers installed and are using a 1st gen kinect.");
        }

        private static void InitNetwork()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var ipString = string.Empty;
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    ipString = ip.ToString();
                    break;
                }
            }
            if (string.IsNullOrEmpty(ipString))
            {
                throw new InvalidOperationException("Something is horribly wrong with ip");
            }

            var segs = ipString.Split('.');

            var broadcastAddress = segs[0] + "." + segs[1] + "." + segs[2] + "." + 255;

            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.EnableBroadcast = true;
            endPoint = new IPEndPoint(IPAddress.Parse(broadcastAddress), 11000);
        }
    }
}
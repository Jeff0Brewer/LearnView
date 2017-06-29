using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using EyeXFramework.Wpf;
using Tobii.EyeX.Framework;
using EyeXFramework;

using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Text.RegularExpressions;
using System.ComponentModel;

namespace TeachView
{
    public partial class MainWindow : Window
    {
        private static string defaultSenderIP = ""; //169.254.41.115 A, 169.254.50.139 B
        private bool SenderOn = false;
        private bool ReceiverOn = true;
        private static int ReceiverPort = 11000, SenderPort = 11000;//ReceiverPort is the port used by Receiver, SenderPort is the port used by Sender
        private bool communication_started_Receiver = false;//indicates whether the Receiver is ready to receive message(coordinates). Used for thread control
        private bool communication_started_Sender = false;//Indicate whether the program is sending its coordinates to others. Used for thread control
        private System.Threading.Thread communicateThread_Receiver; //Thread for receiver
        private System.Threading.Thread communicateThread_Sender;   //Thread for sender
        private static string SenderIP = "", ReceiverIP = ""; //The IP's for sender and receiver.
        private static string IPpat = @"(\d+)(\.)(\d+)(\.)(\d+)(\.)(\d+)\s+"; // regular expression used for matching ip address
        private Regex r = new Regex(IPpat, RegexOptions.IgnoreCase);//regular expression variable
        private static string NumPat = @"(\d+)\s+";
        private Regex regex_num = new Regex(NumPat, RegexOptions.IgnoreCase);
        private static String sending;
        private static String[] received = new String[5];
        private static Point[] receivedPoints = new Point[5];

        EyeXHost eyeXHost;
        Point track = new Point(0, 0);
        
        //Custom scrollbar
        double clickHeight;
        double scrRatio = 0;

        //Tracking
        Scope tr0, tr1;
        //Dot tr0, tr1;

        //Logging
        StringBuilder csv = new StringBuilder();
        String filePath;
        String pathStart = "C:/Users/ResearchSquad/Documents/TeachLog/data";
        int testOffset = 0;
        int testNumber = 0; //Set for each new group

        public MainWindow()
        {
            InitializeComponent();

            //Logging init
            filePath = pathStart + testNumber + ".csv";
            while (File.Exists(filePath))
            {
                testOffset++;
                filePath = pathStart + testNumber + "-" + testOffset.ToString() + ".csv";
            }
            csv.AppendLine("Teacher X,Teacher Y,c0 X,c0 Y,c1 X, c1 Y,Time,Unix Time,Condition,Gaze");

            //Tracking init
            //tr0 = new Dot(System.Windows.Media.Colors.Purple, 5, canv);
            //tr1 = new Dot(System.Windows.Media.Colors.Red, 5, canv);

            tr0 = new Scope(System.Windows.Media.Colors.Purple, 70, 20, canv);
            tr1 = new Scope(System.Windows.Media.Colors.Red, 70, 20, canv);

            scrTrack0.Fill = new SolidColorBrush(System.Windows.Media.Colors.Purple);
            scrTrack1.Fill = new SolidColorBrush(System.Windows.Media.Colors.Red);

            if (ReceiverOn)
            {
                IPHostEntry ipHostInfo = Dns.GetHostByName(Dns.GetHostName());
                IPAddress ipAddress = ipHostInfo.AddressList[0];
                Receive_Status_Text.Text = "Receiving Data at\nIP:" + ipAddress.ToString();
                Receive_Status_Text.Visibility = Visibility.Visible;
            }
            if (SenderOn)
            {
                SenderIP = defaultSenderIP;
                Share_Status_Text.Text = "Sharing Data to\nIP:" + SenderIP.ToString();
                Share_Status_Text.Visibility = Visibility.Visible;
                communication_started_Sender = false;
            }

            eyeXHost = new EyeXHost();
            eyeXHost.Start();
            var gazeData = eyeXHost.CreateGazePointDataStream(GazePointDataMode.LightlyFiltered);
            gazeData.Next += gazePoint;

            System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Render);
            dispatcherTimer.Tick += new EventHandler(update);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 10);
            dispatcherTimer.Start();
        }

        private void logData()
        {
            Point p0 = fromReceived(receivedPoints[0]);
            Point p1 = fromReceived(receivedPoints[1]);
            String newLine = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9}", track.X, track.Y - Canvas.GetTop(bg), p0.X, p0.Y, p1.X, p1.Y,
                                            DateTime.Now.TimeOfDay, DateTimeOffset.Now.ToUnixTimeMilliseconds(),"fill","fill");
            csv.AppendLine(newLine);
        }

        private void gazePoint(object s, EyeXFramework.GazePointEventArgs e)
        {
            track.X = track.X*.9 + e.X*.1;
            track.Y = track.Y*.9 + e.Y*.1;
        }

        private void update(object sender, EventArgs e)
        {
            //Receive
            if (ReceiverOn && communication_started_Receiver == false)
            {
                communication_started_Receiver = true;
                communicateThread_Receiver = new System.Threading.Thread(new ThreadStart(() => tryCommunicateReceiver(sending)));
                communicateThread_Receiver.Start();
            }
            if (SenderOn && communication_started_Sender == false)
            {
                communication_started_Sender = true;
                communicateThread_Sender = new System.Threading.Thread(new ThreadStart(() => tryCommunicateSender(sending)));
                communicateThread_Sender.Start();
            }
            test.Text = received[0] + " " + received[1];

            //Scrolling
            Canvas.SetTop(bg, scrRatio*(Canvas.GetTop(scrollBg) - Canvas.GetTop(scrollHandle)));

            //Tracking
            //tr0.next(fromReceived(receivedPoints[0]), Canvas.GetTop(bg));
            //Canvas.SetTop(scrTrack0, scrollBg.Height * receivedPoints[0].Y);

            //tr1.next(fromReceived(receivedPoints[1]), Canvas.GetTop(bg));
            //Canvas.SetTop(scrTrack1, scrollBg.Height * receivedPoints[1].Y);

            tr0.next(PointFromScreen(track), 0);
            Canvas.SetTop(scrTrack0, scrollBg.Height * (PointFromScreen(track).Y - Canvas.GetTop(bg)) / bg.Height);

            logData();

            //Calibration
            if(scrollWheel.Opacity > 0)
                scrollWheel.Opacity -= .01;
        }

        private Point fromReceived(Point p) {
            double x = p.X * bg.Width;
            double y = p.Y * bg.Height;
            return new Point(x, y);
        }

        private class Scope {
            private Ellipse[] lens;
            private Point[] echo;
            private double radius;
            private double currRad;
            private double stretch;

            public Scope(System.Windows.Media.Color color, double rad, int len, Canvas canv)
            {
                stretch = 3;
                Brush br = new SolidColorBrush(color);
                radius = rad;
                currRad = rad;
                lens = new Ellipse[len];
                echo = new Point[len];
                double opacity = .2;
                double opacityInc = opacity / (len-1);
                for (int i = 0; i < len; i++) {
                    echo[i] = new Point(0, 0);
                    lens[i] = new Ellipse();
                    lens[i].Width = radius * 2;
                    lens[i].Height = radius * 2;
                    lens[i].Stroke = br;
                    lens[i].StrokeThickness = stretch;
                    Canvas.SetLeft(lens[i], 0);
                    Canvas.SetTop(lens[i], 0);
                    lens[i].Opacity = opacity;
                    opacity -= opacityInc;
                    canv.Children.Add(lens[i]);
                }
                lens[0].Opacity = .5;
            }

            public void next(Point p, double offset) {
                double estSpd = Math.Sqrt(Math.Pow(p.X - echo[0].X, 2) + Math.Pow(p.Y - echo[0].Y, 2));
                double rat = estSpd / 50;
                rat = (rat > 1) ? 1 : rat;
                for (int i = echo.Length - 1; i > 0; i--) {
                    echo[i].X = echo[i - 1].X;
                    echo[i].Y = echo[i - 1].Y;
                }
                echo[0].X = p.X;
                echo[0].Y = p.Y;

                Point currPoint = p;
                int currEchoInd = 0;
                Point currEcho = new Point(echo[0].X - currPoint.X, echo[0].Y - currPoint.Y);
                double currEchoDist = 0;
                bool sub = false;
                Canvas.SetLeft(lens[0], currPoint.X - currRad);
                Canvas.SetTop(lens[0], currPoint.Y - currRad + offset);
                lens[0].Width = 2*currRad;
                lens[0].Height = 2*currRad;
                for (int i = 1; i < lens.Length; i++) {
                    lens[i].Width = 2*currRad;
                    lens[i].Height = 2*currRad;
                    if (currEchoDist < stretch && sub)
                    {
                        sub = false;
                        currEchoInd++;
                        currEcho.X = echo[currEchoInd].X - currPoint.X;
                        currEcho.Y = echo[currEchoInd].Y - currPoint.Y;
                        currEchoDist = Math.Sqrt(Math.Pow(currEcho.X, 2) + Math.Pow(currEcho.Y, 2));
                    }
                    if (currEchoDist < stretch && !sub)
                    {
                        //currPoint.X += currEcho.X;
                        //currPoint.Y += currEcho.Y;
                        //Canvas.SetLeft(lens[i], currPoint.X - currRad);
                        //Canvas.SetTop(lens[i], currPoint.Y - currRad);
                        Canvas.SetLeft(lens[i], p.X - currRad);
                        Canvas.SetTop(lens[i], p.Y - currRad + offset);
                        lens[i].Visibility = Visibility.Hidden;
                        currEchoInd++;
                        currEcho.X = echo[currEchoInd].X - currPoint.X;
                        currEcho.Y = echo[currEchoInd].Y - currPoint.Y;
                        currEchoDist = Math.Sqrt(Math.Pow(currEcho.X, 2) + Math.Pow(currEcho.Y, 2));
                    }
                    else
                    {
                        double shiftX = 3 * currEcho.X / currEchoDist;
                        double shiftY = 3 * currEcho.Y / currEchoDist;
                        currPoint.X += shiftX;
                        currPoint.Y += shiftY;
                        Canvas.SetLeft(lens[i], currPoint.X - currRad);
                        Canvas.SetTop(lens[i], currPoint.Y - currRad + offset);
                        lens[i].Visibility = Visibility.Visible;
                        currEchoDist -= stretch;
                        currEcho.X -= shiftX;
                        currEcho.Y -= shiftY;
                        sub = true;
                    }
                }
            }
        }

        private class Dot {
            private Ellipse body;
            private Line[] trail;
            private Point[] echo;
            private int radius;

            public Dot(System.Windows.Media.Color color, int len, Canvas canv) {
                radius = 5;
                Brush br = new SolidColorBrush(color);
                body = new Ellipse();
                Canvas.SetTop(body, 0);
                Canvas.SetLeft(body, 0);
                body.Width = radius * 2;
                body.Height = radius * 2;
                body.Fill = br;
                canv.Children.Add(body);
                trail = new Line[len];
                echo = new Point[len + 1];
                echo[len] = new Point(0, 0);
                double thickness = radius * 2;
                double thickInc = thickness / len;
                for (int i = 0; i < len; i++) {
                    echo[i] = new Point(0, 0);
                    trail[i] = new Line();
                    trail[i].X1 = radius;
                    trail[i].X2 = radius;
                    trail[i].Y1 = radius;
                    trail[i].Y2 = radius;
                    trail[i].StrokeThickness = thickness;
                    trail[i].Stroke = br;
                    trail[i].Opacity = .5;
                    canv.Children.Add(trail[i]);
                    thickness -= thickInc;
                }
            }

            public void next(Point p, double offset) {
                Canvas.SetLeft(body, p.X - radius);
                Canvas.SetTop(body, p.Y - radius + offset);
                for (int i = echo.Length - 1; i > 0; i--) {
                    echo[i].X = echo[i - 1].X;
                    echo[i].Y = echo[i - 1].Y;
                }
                echo[0].X = p.X;
                echo[0].Y = p.Y;
                for (int i = 0; i < trail.Length; i++) {
                    trail[i].X1 = echo[i].X;
                    trail[i].Y1 = echo[i].Y + offset;
                    trail[i].X2 = echo[i + 1].X;
                    trail[i].Y2 = echo[i + 1].Y + offset;
                }
            }
        }

        private void checkCal(object sender, MouseButtonEventArgs e)
        {
            Point fromScreen = PointFromScreen(track);
            if (dist(fromScreen, e.GetPosition(topleft)) < 70)
            {
                scrollWheel.Opacity = .25;
                scrollWheel.Fill = new SolidColorBrush(System.Windows.Media.Colors.CornflowerBlue);
            }
            else
            {
                scrollWheel.Opacity = .25;
                scrollWheel.Fill = new SolidColorBrush(System.Windows.Media.Colors.Red);
            }
        }

        private double dist(Point a, Point b) {
            return Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            File.WriteAllText(filePath, csv.ToString());
            eyeXHost.Dispose();
        }

        private void scrClick(object sender, MouseButtonEventArgs e)
        {
            Canvas.SetLeft(scrollHover, 0);
            clickHeight = e.GetPosition(topleft).Y - Canvas.GetTop(scrollHandle);
        }

        private void scrMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            double h = e.GetPosition(topleft).Y - clickHeight;
            h = (h < Canvas.GetTop(scrollBg) + scrollBg.Height - scrollHandle.Height) ? h : Canvas.GetTop(scrollBg) + scrollBg.Height - scrollHandle.Height;
            Canvas.SetTop(scrollHandle, (h > 0) ? h : 0);
        }

        private void scrUnclick(object sender, MouseButtonEventArgs e)
        {
            Canvas.SetLeft(scrollHover, -2000);
        }

        private void scrWheel(object sender, MouseWheelEventArgs e)
        {
            double h = Canvas.GetTop(scrollHandle) - e.Delta / 2;
            h = (h < Canvas.GetTop(scrollBg) + scrollBg.Height - scrollHandle.Height) ? h : Canvas.GetTop(scrollBg) + scrollBg.Height - scrollHandle.Height;
            Canvas.SetTop(scrollHandle, (h > 0) ? h : 0);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            double ratio = bg.Height / bg.Width;
            double availableWidth = this.ActualWidth - SystemParameters.WindowNonClientFrameThickness.Left - SystemParameters.WindowNonClientFrameThickness.Right - scrollBg.Width;
            bg.Width = availableWidth;
            bg.Height = availableWidth * ratio;
            double availableHeight = this.ActualHeight - SystemParameters.WindowNonClientFrameThickness.Top - SystemParameters.WindowNonClientFrameThickness.Bottom;
            scrollBg.Height = availableHeight;
            scrollHandle.Height = (availableHeight / bg.Height) * scrollBg.Height;
            scrRatio = (bg.Height - availableHeight) /(scrollBg.Height - scrollHandle.Height);
            
        }

        #region socket stuff
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            //CleanUp();
            //SenderOn = false;
            //ReceiverOn = false;
            communication_started_Receiver = false;
            communication_started_Sender = false;
            //dispatcherTimer.Stop();
            //eyeXHost.Dispose();
            try
            {
                communicateThread_Receiver.Abort();
                communicateThread_Sender.Abort();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            base.OnClosing(e);

        }

        public void tryCommunicateReceiver(String x)
        {
            IPHostEntry ipHostInfo = Dns.Resolve(Dns.GetHostName());
            ReceiverIP = ipHostInfo.AddressList[0].ToString();

            while (ReceiverIP == "")
            {
                System.Threading.Thread.Sleep(1000);
            }
            AsynchronousSocketListener.StartListening();
        }
        public void tryCommunicateSender(String x)
        {
            while (SenderIP == "")
            {
                System.Threading.Thread.Sleep(1000);
            }
            SynchronousClient.StartClient(x); //start sending info
            communication_started_Sender = false;

            //AsynchronousSocketListener.StartListening();
        }

        public class StateObject
        {
            // Client  socket.
            public Socket workSocket = null;
            // Size of receive buffer.
            public const int BufferSize = 1024;
            // Receive buffer.
            public byte[] buffer = new byte[BufferSize];
            // Received data string.
            public StringBuilder sb = new StringBuilder();
        }
        //THis is the Receiver function (Asyncronous)
        // Citation: https://msdn.microsoft.com/en-us/library/fx6588te%28v=vs.110%29.aspx
        public class AsynchronousSocketListener
        {
            // Thread signal.
            public static ManualResetEvent allDone = new ManualResetEvent(false);
            public AsynchronousSocketListener()
            {
            }
            public static void StartListening()
            {
                if (ReceiverIP != "")
                {
                    // Data buffer for incoming data.
                    byte[] bytes = new Byte[1024];

                    // Establish the local endpoint for the socket.
                    // The DNS name of the computer
                    IPHostEntry ipHostInfo = Dns.Resolve(Dns.GetHostName());
                    IPAddress ipAddress = IPAddress.Parse(ReceiverIP);
                    IPEndPoint localEndPoint = new IPEndPoint(ipAddress, ReceiverPort);

                    // Create a TCP/IP socket.
                    Socket listener = new Socket(AddressFamily.InterNetwork,
                        SocketType.Stream, ProtocolType.Tcp);

                    // Bind the socket to the local endpoint and listen for incoming connections.
                    try
                    {
                        listener.Bind(localEndPoint);
                        listener.Listen(100);
                        //ommunication_received==false
                        while (true)
                        {
                            // Set the event to nonsignaled state.
                            allDone.Reset();

                            // Start an asynchronous socket to listen for connections.
                            //Console.WriteLine("Waiting for a connection...");
                            listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);

                            allDone.WaitOne();

                            // Wait until a connection is made before continuing.
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.ToString());
                    }
                    //Console.WriteLine("\nPress ENTER to continue...");
                    //Console.Read();
                }
            }
            public static void AcceptCallback(IAsyncResult ar)
            {
                // Signal the main thread to continue.
                allDone.Set();

                // Get the socket that handles the client request.
                Socket listener = (Socket)ar.AsyncState;
                Socket handler = listener.EndAccept(ar);

                // Create the state object.
                StateObject state = new StateObject();
                state.workSocket = handler;
                handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReadCallback), state);
            }
            public static void ReadCallback(IAsyncResult ar)
            {
                String content = String.Empty;
                // Retrieve the state object and the handler socket
                // from the asynchronous state object.
                StateObject state = (StateObject)ar.AsyncState;
                Socket handler = state.workSocket;

                // Read data from the client socket. 
                int bytesRead = handler.EndReceive(ar);

                if (bytesRead > 0)
                {
                    // There  might be more data, so store the data received so far.
                    state.sb.Append(Encoding.ASCII.GetString(
                        state.buffer, 0, bytesRead));

                    // Check for end-of-file tag. If it is not there, read more data.
                    content = state.sb.ToString();
                    if (content.IndexOf("<EOF>") > -1)
                    {
                        // All the data has been read from the client. Display it on the console.
                        int x_start_ind = content.IndexOf("x: "), x_end_ind = content.IndexOf("xend ");
                        // int x_start_ind = content.IndexOf("x: "), x_end_ind = content.IndexOf("xend ");
                        // int y_start_ind = content.IndexOf("y: "), y_end_ind = content.IndexOf("yend ");

                        if (x_start_ind > -1 && x_end_ind > -1)
                        {
                            try
                            {
                                //convert the received string into x and y                                
                                // x_received = Convert.ToInt32(content.Substring(x_start_ind + 3, x_end_ind - (x_start_ind + 3)));
                                // y_received = Convert.ToInt32(content.Substring(y_start_ind + 3, y_end_ind - (y_start_ind + 3)));
                                string s = content.Substring(x_start_ind + 3, x_end_ind - (x_start_ind + 3));
                                //received_cards_arr = s.Split(',').Select(str => int.Parse(str)).ToArray(); ;
                                // received = Convert.ToInt32(content.Substring(x_start_ind + 3, x_end_ind - (x_start_ind + 3)));
                                int ind = Convert.ToInt32(s.Substring(1, 1));
                                received[ind] = s;
                                receivedPoints[ind].X = Convert.ToDouble(s.Substring(2, s.IndexOf("|") - 2));
                                receivedPoints[ind].Y = Convert.ToDouble(s.Substring(s.IndexOf("|") + 1));
                            }
                            catch (FormatException)
                            {
                                Console.WriteLine("Input string is not a sequence of digits.");
                            }
                            catch (OverflowException)
                            {
                                Console.WriteLine("The number cannot fit in an Int32.");
                            }
                        }
                        // Show the data on the console.
                        //Console.WriteLine("x : {0}  y: {1}", x_received, y_received);

                        // Echo the data back to the client.
                        Send(handler, content);
                    }
                    else
                    {
                        // Not all data received. Get more.
                        handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                        new AsyncCallback(ReadCallback), state);
                    }
                }
            }

            private static void Send(Socket handler, String data)
            {
                // Convert the string data to byte data using ASCII encoding.
                byte[] byteData = Encoding.ASCII.GetBytes(data);

                // Begin sending the data to the remote device.
                handler.BeginSend(byteData, 0, byteData.Length, 0,
                    new AsyncCallback(SendCallback), handler);
            }

            private static void SendCallback(IAsyncResult ar)
            {
                try
                {
                    // Retrieve the socket from the state object.
                    Socket handler = (Socket)ar.AsyncState;

                    // Complete sending the data to the remote device.
                    int bytesSent = handler.EndSend(ar);
                    //Console.WriteLine("Sent {0} bytes to client.", bytesSent);x

                    handler.Shutdown(SocketShutdown.Both);
                    handler.Close();

                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
        }
        //This is the sending function (Syncronous)
        public class SynchronousClient
        {

            public static void StartClient(String x)
            {
                // Data buffer for incoming data.
                byte[] bytes = new byte[1024];

                // Connect to a remote device.
                try
                {
                    // Establish the remote endpoint for the socket.
                    // This example uses port 11000 on the local computer.
                    IPHostEntry ipHostInfo = Dns.GetHostByName(Dns.GetHostName());
                    IPAddress ipAddress = IPAddress.Parse(SenderIP);
                    IPEndPoint remoteEP = new IPEndPoint(ipAddress, SenderPort);

                    // Create a TCP/IP  socket.
                    Socket sender = new Socket(AddressFamily.InterNetwork,
                        SocketType.Stream, ProtocolType.Tcp);

                    // Connect the socket to the remote endpoint. Catch any errors.
                    try
                    {
                        sender.Connect(remoteEP);

                        Console.WriteLine("Socket connected to {0}",
                            sender.RemoteEndPoint.ToString());
                        //
                        string array_to_string = string.Join(",", x);
                        string message_being_sent = "x: " + x + "xend <EOF>";
                        //string message_being_sent = "x: " + x + "xend y: " + y + "yend cursorx: " +
                        //    System.Windows.Forms.Cursor.Position.X + "cursorxend cursory: " +
                        //    System.Windows.Forms.Cursor.Position.Y + "cursoryend <EOF>";
                        // Encode the data string into a byte array.
                        byte[] msg = Encoding.ASCII.GetBytes(message_being_sent);

                        // Send the data through the socket.
                        int bytesSent = sender.Send(msg);

                        // Receive the response from the remote device.
                        int bytesRec = sender.Receive(bytes);
                        Console.WriteLine("Echoed test = {0}",
                            Encoding.ASCII.GetString(bytes, 0, bytesRec));

                        // Release the socket.
                        sender.Shutdown(SocketShutdown.Both);
                        sender.Close();

                    }
                    catch (ArgumentNullException ane)
                    {
                        Console.WriteLine("ArgumentNullException : {0}", ane.ToString());
                    }
                    catch (SocketException se)
                    {
                        Console.WriteLine("SocketException : {0}", se.ToString());
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Unexpected exception : {0}", e.ToString());
                    }

                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }

            public static string data = null;
        }
        #endregion
    }
}

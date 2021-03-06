﻿using System;
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

namespace StudentView
{
    public partial class MainWindow : Window
    {
        private int studentNum = 2; //0, 1, or 2
        private static string defaultSenderIP = "192.168.2.6";
        private bool SenderOn = true;
        private static int ReceiverPort = 12000, SenderPort = 11000;//ReceiverPort is the port used by Receiver, SenderPort is the port used by Sender
        private bool communication_started_Sender = false;//Indicate whether the program is sending its coordinates to others. Used for thread control
        private System.Threading.Thread communicateThread_Sender;   //Thread for sender
        private static string SenderIP = "", ReceiverIP = ""; //The IP's for sender and receiver.
        private static string IPpat = @"(\d+)(\.)(\d+)(\.)(\d+)(\.)(\d+)\s+"; // regular expression used for matching ip address
        private Regex r = new Regex(IPpat, RegexOptions.IgnoreCase);//regular expression variable
        private static string NumPat = @"(\d+)\s+";
        private Regex regex_num = new Regex(NumPat, RegexOptions.IgnoreCase);
        private System.Windows.Threading.DispatcherTimer dispatcherTimer;
        private static String sending;
        private static String[] received = new String[5];

        EyeXHost eyeXHost;
        Point track = new Point(0, 0);
        Point toSend = new Point(0, 0);

        //Custom scrollbar
        double clickHeight;
        double scrRatio = 0;

        public MainWindow()
        {
            InitializeComponent();
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

            // Set up timer
            dispatcherTimer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Render);
            dispatcherTimer.Tick += new EventHandler(update);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 10);
            dispatcherTimer.Start();

            // Set up indicator of computer number
            switch(studentNum)
            {
                case 0:
                    StudentIndicator.Content = "Student 0";
                    StudentIndicator.Background = new SolidColorBrush(System.Windows.Media.Colors.Purple);
                    break;
                case 1:
                    StudentIndicator.Content = "Student 1";
                    StudentIndicator.Background = new SolidColorBrush(System.Windows.Media.Colors.Red);
                    break;
                case 2:
                    StudentIndicator.Content = "Student 2";
                    StudentIndicator.Background = new SolidColorBrush(System.Windows.Media.Colors.CornflowerBlue);
                    break;                   
            }
        }

        /*
         * Update gaze point (track) from new gaze position.
         */
        private void gazePoint(object s, EyeXFramework.GazePointEventArgs e)
        {
            track.X = track.X*.8 + e.X*.2;
            track.Y = track.Y*.8 + e.Y*.2;
        }

        private void update(object sender, EventArgs e)
        {
            //Scrolling
            Canvas.SetTop(bg, scrRatio * (Canvas.GetTop(scrollBg) - Canvas.GetTop(scrollHandle)));

            Point fromScreen = PointFromScreen(track);
            //Canvas.SetTop(tempTrack, fromScreen.Y);
            //Canvas.SetLeft(tempTrack, fromScreen.X);

            toSend.X = fromScreen.X / bg.Width;
            toSend.Y = (fromScreen.Y - Canvas.GetTop(bg)) / bg.Height;

            test.Text = toSend.ToString();

            //Sending
            sending = "c" + studentNum + toSend.X.ToString() + "|" + toSend.Y.ToString();

            //If user pressed Sender button but communication haven't started yet or has terminated, start a thread on tryCommunicateSender()
            if (SenderOn && communication_started_Sender == false)
            {
                communication_started_Sender = true;
                communicateThread_Sender = new System.Threading.Thread(new ThreadStart(() => tryCommunicateSender(sending)));
                communicateThread_Sender.Start();
            }
        }

        #region Scroll Methods
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

        private void clickableScrollBg_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point p = e.GetPosition(this);
            double h = (p.Y - scrollHandle.Height / 2) < 0 ? 0 : (p.Y - scrollHandle.Height / 2);
            double posn = h + scrollHandle.Height / 2 - scrollBg.Height;
            h = (h < Canvas.GetTop(scrollBg) + scrollBg.Height - scrollHandle.Height) ? h : Canvas.GetTop(scrollBg) + scrollBg.Height - scrollHandle.Height;
            Canvas.SetTop(scrollHandle, (h > 0) ? h : 0);

            //move background
            Canvas.SetTop(bg, scrRatio * (Canvas.GetTop(scrollBg) - Canvas.GetTop(scrollHandle)));
        }
        #endregion

        /*
         * Set up window and scroll dimensions
         */
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            double ratio = bg.Height / bg.Width;
            double availableWidth = this.ActualWidth - SystemParameters.WindowNonClientFrameThickness.Left - SystemParameters.WindowNonClientFrameThickness.Right - scrollBg.Width;
            bg.Width = availableWidth;
            bg.Height = availableWidth * ratio;
            double availableHeight = this.ActualHeight - SystemParameters.WindowNonClientFrameThickness.Top - SystemParameters.WindowNonClientFrameThickness.Bottom;
            scrollBg.Height = availableHeight;
            clickableScrollBg.Height = availableHeight;
            scrollHandle.Height = (availableHeight / bg.Height) * scrollBg.Height;
            scrRatio = (bg.Height - availableHeight) / (scrollBg.Height - scrollHandle.Height);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            //CleanUp();
            communication_started_Sender = false;
            //dispatcherTimer.Stop();
            //eyeXHost.Dispose();
            try
            {
                communicateThread_Sender.Abort();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            base.OnClosing(e);

        }

        #region socket stuff
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
                                received[0] = s;
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

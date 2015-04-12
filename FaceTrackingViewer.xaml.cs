// --------------------------------------------------------------------------------------------------------------------
// <copyright file="FaceTrackingViewer.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace FaceTrackingBasics
{
    using System;
    using System.IO; //for TextWriter
    using System.Text; //stringbuilder 
    using System.Collections; //for arrayList
   
    using System.Threading;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using Microsoft.Kinect;
    using Microsoft.Kinect.Toolkit.FaceTracking;


    using Point = System.Windows.Point;

    /// <summary>
    /// Class that uses the Face Tracking SDK to display a face mask for
    /// tracked skeletons
    /// </summary>
    public partial class FaceTrackingViewer : UserControl, IDisposable
    {
        public static readonly DependencyProperty KinectProperty = DependencyProperty.Register(
            "Kinect", 
            typeof(KinectSensor), 
            typeof(FaceTrackingViewer), 
            new PropertyMetadata(
                null, (o, args) => ((FaceTrackingViewer)o).OnSensorChanged((KinectSensor)args.OldValue, (KinectSensor)args.NewValue)));

        private const uint MaxMissedFrames = 100;

        private readonly Dictionary<int, SkeletonFaceTracker> trackedSkeletons = new Dictionary<int, SkeletonFaceTracker>();

        private byte[] colorImage;

        private ColorImageFormat colorImageFormat = ColorImageFormat.Undefined;

        private short[] depthImage;

        private DepthImageFormat depthImageFormat = DepthImageFormat.Undefined;

        private bool disposed;

        private Skeleton[] skeletonData;

        //personal class variables
        static int numberOfFrames = 0;
        static int frameIter = 1;
        static int sampleRate = 30; //50 - roughly every 1.5 seconds
        static Stopwatch timer = new Stopwatch();//for sake of knowing how quick samples are taken
        static Boolean timerStarted = false;
        const String filePath = "F:\\cygwin\\home\\aok5326\\workspace\\FacialRecognition\\data.csv";
        static List<float> sampleOneDistances = new List<float>();
        static List<float> sampleTwoDistances = new List<float>();
        static List<float> sampleThreeDistances = new List<float>();
        static List<float> sampleFourDistances = new List<float>();
        static int[] sampleOneHistogram = new int[65];//64 bins
        static float sampleOneMaxDistance = 0;
        static int[] sampleTwoHistogram = new int[65];
        static float sampleTwoMaxDistance = 0;
        static int[] sampleThreeHistogram = new int[65];
        static float sampleThreeMaxDistance = 0;
        static int[] sampleFourHistogram = new int[65];
        static float sampleFourMaxDistance = 0;
        static ArrayList pointList = new ArrayList();


        public FaceTrackingViewer()
        {
            this.InitializeComponent();
        }

        ~FaceTrackingViewer()
        {
            this.Dispose(false);
        }

        public KinectSensor Kinect
        {
            get
            {
                return (KinectSensor)this.GetValue(KinectProperty);
            }

            set
            {
                this.SetValue(KinectProperty, value);
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                this.ResetFaceTracking();

                this.disposed = true;
            }
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            foreach (SkeletonFaceTracker faceInformation in this.trackedSkeletons.Values)
            {
                faceInformation.DrawFaceModel(drawingContext);
            }
        }

        private void OnAllFramesReady(object sender, AllFramesReadyEventArgs allFramesReadyEventArgs)
        {
            ColorImageFrame colorImageFrame = null;
            DepthImageFrame depthImageFrame = null;
            SkeletonFrame skeletonFrame = null;

            try
            {
                colorImageFrame = allFramesReadyEventArgs.OpenColorImageFrame();
                depthImageFrame = allFramesReadyEventArgs.OpenDepthImageFrame();
                skeletonFrame = allFramesReadyEventArgs.OpenSkeletonFrame();

                if (colorImageFrame == null || depthImageFrame == null || skeletonFrame == null)
                {
                    return;
                }

                // Check for image format changes.  The FaceTracker doesn't
                // deal with that so we need to reset.
                if (this.depthImageFormat != depthImageFrame.Format)
                {
                    this.ResetFaceTracking();
                    this.depthImage = null;
                    this.depthImageFormat = depthImageFrame.Format;
                }

                if (this.colorImageFormat != colorImageFrame.Format)
                {
                    this.ResetFaceTracking();
                    this.colorImage = null;
                    this.colorImageFormat = colorImageFrame.Format;
                }

                // Create any buffers to store copies of the data we work with
                if (this.depthImage == null)
                {
                    this.depthImage = new short[depthImageFrame.PixelDataLength];
                }

                if (this.colorImage == null)
                {
                    this.colorImage = new byte[colorImageFrame.PixelDataLength];
                }
                
                // Get the skeleton information
                if (this.skeletonData == null || this.skeletonData.Length != skeletonFrame.SkeletonArrayLength)
                {
                    this.skeletonData = new Skeleton[skeletonFrame.SkeletonArrayLength];
                }

                colorImageFrame.CopyPixelDataTo(this.colorImage);
                depthImageFrame.CopyPixelDataTo(this.depthImage);
                skeletonFrame.CopySkeletonDataTo(this.skeletonData);

                // Update the list of trackers and the trackers with the current frame information
                foreach (Skeleton skeleton in this.skeletonData)
                {
                    if (skeleton.TrackingState == SkeletonTrackingState.Tracked
                        || skeleton.TrackingState == SkeletonTrackingState.PositionOnly)
                    {
                        // We want keep a record of any skeleton, tracked or untracked.
                        if (!this.trackedSkeletons.ContainsKey(skeleton.TrackingId))
                        {
                            this.trackedSkeletons.Add(skeleton.TrackingId, new SkeletonFaceTracker());
                        }

                        // Give each tracker the upated frame.
                        SkeletonFaceTracker skeletonFaceTracker;
                        if (this.trackedSkeletons.TryGetValue(skeleton.TrackingId, out skeletonFaceTracker))
                        {
                            skeletonFaceTracker.OnFrameReady(this.Kinect, colorImageFormat, colorImage, depthImageFormat, depthImage, skeleton);
                            skeletonFaceTracker.LastTrackedFrame = skeletonFrame.FrameNumber;
                        }
                    }
                }

                this.RemoveOldTrackers(skeletonFrame.FrameNumber);

                this.InvalidateVisual();
            }
            finally
            {
                if (colorImageFrame != null)
                {
                    colorImageFrame.Dispose();
                }

                if (depthImageFrame != null)
                {
                    depthImageFrame.Dispose();
                }

                if (skeletonFrame != null)
                {
                    skeletonFrame.Dispose();
                }
            }
        }

        private void OnSensorChanged(KinectSensor oldSensor, KinectSensor newSensor)
        {
            if (oldSensor != null)
            {
                oldSensor.AllFramesReady -= this.OnAllFramesReady;
                this.ResetFaceTracking();
            }

            if (newSensor != null)
            {
                newSensor.AllFramesReady += this.OnAllFramesReady;
            }
        }

        /// <summary>
        /// Clear out any trackers for skeletons we haven't heard from for a while
        /// </summary>
        private void RemoveOldTrackers(int currentFrameNumber)
        {
            var trackersToRemove = new List<int>();

            foreach (var tracker in this.trackedSkeletons)
            {
                uint missedFrames = (uint)currentFrameNumber - (uint)tracker.Value.LastTrackedFrame;
                if (missedFrames > MaxMissedFrames)
                {
                    // There have been too many frames since we last saw this skeleton
                    trackersToRemove.Add(tracker.Key);
                }
            }

            foreach (int trackingId in trackersToRemove)
            {
                this.RemoveTracker(trackingId);
            }
        }

        private void RemoveTracker(int trackingId)
        {
            this.trackedSkeletons[trackingId].Dispose();
            this.trackedSkeletons.Remove(trackingId);
        }

        private void ResetFaceTracking()
        {
            foreach (int trackingId in new List<int>(this.trackedSkeletons.Keys))
            {
                this.RemoveTracker(trackingId);
            }
        }

        private class SkeletonFaceTracker : IDisposable
        {
            private static FaceTriangle[] faceTriangles;

            private EnumIndexableCollection<FeaturePoint, PointF> facePoints;

            private FaceTracker faceTracker;

            private bool lastFaceTrackSucceeded;

            private SkeletonTrackingState skeletonTrackingState;

            public int LastTrackedFrame { get; set; }

            public void Dispose()
            {
                if (this.faceTracker != null)
                {
                    this.faceTracker.Dispose();
                    this.faceTracker = null;
                }
            }

            public void DrawFaceModel(DrawingContext drawingContext)
            {
                if (!this.lastFaceTrackSucceeded || this.skeletonTrackingState != SkeletonTrackingState.Tracked)
                {
                    return;
                }

                var faceModelPts = new List<Point>();
                var faceModel = new List<FaceModelTriangle>();

                for (int i = 0; i < this.facePoints.Count; i++)
                {
                    faceModelPts.Add(new Point(this.facePoints[i].X + 0.5f, this.facePoints[i].Y + 0.5f));
                }

                foreach (var t in faceTriangles)
                {
                    var triangle = new FaceModelTriangle();
                    triangle.P1 = faceModelPts[t.First];
                    triangle.P2 = faceModelPts[t.Second];
                    triangle.P3 = faceModelPts[t.Third];
                    faceModel.Add(triangle);
                }

                var faceModelGroup = new GeometryGroup();
                for (int i = 0; i < faceModel.Count; i++)
                {
                    var faceTriangle = new GeometryGroup();
                    faceTriangle.Children.Add(new LineGeometry(faceModel[i].P1, faceModel[i].P2));
                    faceTriangle.Children.Add(new LineGeometry(faceModel[i].P2, faceModel[i].P3));
                    faceTriangle.Children.Add(new LineGeometry(faceModel[i].P3, faceModel[i].P1));
                    faceModelGroup.Children.Add(faceTriangle);
                }

                drawingContext.DrawGeometry(Brushes.LightYellow, new Pen(Brushes.LightYellow, 1.0), faceModelGroup);
            }

            /// <summary>
            /// Updates the face tracking information for this skeleton
            /// </summary>
            /// 
            static Boolean once = false;
            internal void OnFrameReady(KinectSensor kinectSensor, ColorImageFormat colorImageFormat, byte[] colorImage, DepthImageFormat depthImageFormat, short[] depthImage, Skeleton skeletonOfInterest)
            {
                if (!timerStarted)
                {
                    timer.Start();
                    timerStarted = true;
                }
                //increment our frames
                numberOfFrames++;


                this.skeletonTrackingState = skeletonOfInterest.TrackingState;

                if (this.skeletonTrackingState != SkeletonTrackingState.Tracked)
                {
                    // nothing to do with an untracked skeleton.
                    return;
                }

                if (this.faceTracker == null)
                {
                    try
                    {
                        this.faceTracker = new FaceTracker(kinectSensor);
                    }
                    catch (InvalidOperationException)
                    {
                        // During some shutdown scenarios the FaceTracker
                        // is unable to be instantiated.  Catch that exception
                        // and don't track a face.
                        Debug.WriteLine("AllFramesReady - creating a new FaceTracker threw an InvalidOperationException");
                        this.faceTracker = null;
                    }
                }

                if (this.faceTracker != null)
                {
                    FaceTrackFrame frame = this.faceTracker.Track(
                        colorImageFormat, colorImage, depthImageFormat, depthImage, skeletonOfInterest);

                    this.lastFaceTrackSucceeded = frame.TrackSuccessful;
                    if (this.lastFaceTrackSucceeded)
                    {
                        if (faceTriangles == null)
                        {
                            // only need to get this once.  It doesn't change.
                            faceTriangles = frame.GetTriangles();
                        }

                        this.facePoints = frame.GetProjected3DShape();
                        
                    /*if ()
                    {
                        Debug.WriteLine("hit " + (frameIter * sampleRate) + " frames in " + (timer.Elapsed) + " seconds");
                        frameIter++;
                    }*/

                        //Also grab our points
                        EnumIndexableCollection<FeaturePoint, Vector3DF> facePoints3D = frame.Get3DShape();
                        //pwd
                        int index = 0;
                        if (numberOfFrames > frameIter * sampleRate && frameIter < 5) //only grab 4 samples
                        {
                            //Create a new thread so we don't make the visual thread throw up all over the place
                            new Thread(() =>
                            {
                            Thread.CurrentThread.IsBackground = true;
   
                            List<Tuple<float, float, float>> myPoints = new List<Tuple<float, float, float>>();
                            foreach (Vector3DF vector in facePoints3D)
                            {
                                //csv.Append(string.Format("( ({1}, {2}, {3}){4}",vector.X, vector.Y, vector.Z, Environment.NewLine));
                                myPoints.Add(new Tuple<float, float, float>(vector.X, vector.Y, vector.Z));
                                index++;
                            }
                            calculateDistances(myPoints);
                            frameIter++;
                            }).Start();
                            //once = true;
                        }

                        if (frameIter == 4)
                        {
                            Console.WriteLine("We are ready to sample");
                            foreach (float distance in sampleOneDistances)
                            {
                                sampleOneHistogram[(int) Math.Floor(64 * distance / sampleOneMaxDistance)]++;
                            }
                            foreach (float distance in sampleTwoDistances)
                            {
                                sampleTwoHistogram[(int)Math.Floor(64 * distance / sampleTwoMaxDistance)]++;
                            }
                            foreach (float distance in sampleThreeDistances)
                            {
                                sampleThreeHistogram[(int)Math.Floor(64 * distance / sampleThreeMaxDistance)]++;
                            }
                            foreach (float distance in sampleFourDistances)
                            {
                                sampleFourHistogram[(int)Math.Floor(64 * distance / sampleFourMaxDistance)]++;
                            }                



                            Console.Write("Sample two size is " + sampleTwoDistances.Count);
                            Console.Write("Sample two max distance is " + sampleTwoMaxDistance);
                            int iter = 0;

                            foreach (int count in sampleTwoHistogram)
                            {
                                Console.WriteLine("S2Count at " + iter + " is " + count + "/" + sampleOneHistogram[iter] + "/" + sampleThreeHistogram[iter] + "/" + sampleFourHistogram);
                                iter++;
                            }
    


                            frameIter++; //only do this once (will make conditional evaluate to false
                        }
                    }
                }
            }

            void calculateDistances(List<Tuple<float, float, float>> myPoints)
            {
                                            ///Iterate through points, calculate difference
                            for (int i = 0; i < 121; i++) 
                            {
                                for (int j = 1; j < 121; j++)//always running one ahead
                                {
                                    float diffX =  (myPoints[i].Item1 - myPoints[j].Item1);
                                    float diffY = (myPoints[i].Item2 - myPoints[j].Item2);
                                    float diffZ = (myPoints[i].Item3 - myPoints[j].Item3);
                                    float distance = (float)Math.Sqrt(diffX * diffX + diffY * diffY + diffZ * diffZ);
                                    var csv = new StringBuilder();
                                    if (frameIter == 1) //sample 1
                                    {
                                        sampleOneDistances.Add(distance);
                                        if (distance > sampleOneMaxDistance)
                                            sampleOneMaxDistance = distance;
                                        //csv.Append(string.Format("{0},{1},{2}, {3}", diffX, diffY, diffZ, Environment.NewLine));//may not want to hardcode that newline
                                        //File.WriteAllText(filePath, csv.ToString
                                    }
                                    else if (frameIter == 2)//sample 2
                                    {
                                        sampleTwoDistances.Add(distance);
                                        if (distance > sampleTwoMaxDistance)
                                            sampleTwoMaxDistance = distance;
                                        //csv.Append(string.Format(",,,{0},{1},{2}, {3}", diffX, diffY, diffZ, Environment.NewLine));//may not want to hardcode that newline
                                        //File.AppendAllText(filePath, csv.ToString(), Encoding.ASCII);//append is true
      
                                    }
                                    else if (frameIter == 3)//sample 3
                                    {
                                        sampleThreeDistances.Add(distance);
                                        if (distance > sampleThreeMaxDistance)
                                            sampleThreeMaxDistance = distance;
                                        //csv.Append(string.Format(",,,,,,{0},{1},{2}, {3}", diffX, diffY, diffZ, Environment.NewLine));//may not want to hardcode that newline
                                       // File.AppendAllText(filePath, csv.ToString(), Encoding.ASCII);//append is true

                                    }
                                    else if (frameIter == 4) //sample 4
                                    {
                                        if (distance > sampleFourMaxDistance)
                                            sampleFourMaxDistance = distance;
                                        sampleFourDistances.Add(distance);
                                    }

                                }
                            }

            }



            private struct FaceModelTriangle
            {
                public Point P1;
                public Point P2;
                public Point P3;
            }
        }
    }
}
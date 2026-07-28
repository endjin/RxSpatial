using System;
using System.IO;
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

using Microsoft.Maps.MapControl.WPF;
using Microsoft.Maps.MapControl.WPF.Design;

namespace MapRunRxSpatial
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        SimulationFromText simFromTxt;
        PolygonMgr polygonMgr;
        MovingObjMgr movingObjMgr;
        public MainWindow()
        {
            InitializeComponent();
            myMap.MouseLeftButtonUp += new MouseButtonEventHandler(Map_MouseLeftButtonUp);
            myMap.MouseMove += new MouseEventHandler(Map_MouseMove);
            //myMap.MouseLeftButtonDown += new MouseButtonEventHandler(Map_MouseLeftButtonDown);
            //SubscribeParams.init();
            //LoadParams.init();
            polygonMgr = new PolygonMgr(myMap);
            //simFromTxt = new SimulationFromText(myMap);
            movingObjMgr = new MovingObjMgr(myMap, polygonMgr);
            //myMap.Children.Add(new UIElement()); in iRoad code mainwindow.xaml
            //scalar bar

        }

        private void Sim_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("console");
            simFromTxt.procTraffic();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
        }
        private void AggregateQuery_OK_Button_Click(object sender, RoutedEventArgs e)
        {
            movingObjMgr.procTraffic();

        }
        private bool collect_area = false;
        private Location pt1 = null;
        private Location pt2 = null;
        private Location pttmp = null;
        MapPolyline tmpPolyline = new MapPolyline();
        private void AddAreaBtn_Click(object sender, RoutedEventArgs e)
        {
            tmpPolyline = new MapPolyline();
            collect_area = true;
            pt1 = null;
            pt2 = null;
        }
        void Map_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (collect_area == true)
            {
                Point mousePosition = e.GetPosition(this);
                //Convert the mouse coordinates to a locatoin on the map
                Location loc = myMap.ViewportPointToLocation(mousePosition);
                if (pt1 == null)
                    pt1 = loc;
                else if (pt2 == null)
                {
                    pt2 = loc; 
                    myMap.Children.Remove(tmpPolyline);
                    addArea(pt1,pt2);
                    collect_area = false;
                    pt1 = null;
                    pt2 = null;
                }
            }

         }
        //TextWriter tw = new StreamWriter(Parameters.dataPath + "collect.txt");
        void Map_MouseMove(object sender, MouseEventArgs e)
        {
            if (collect_area == true)
            {
                Point mousePosition = e.GetPosition(this);
                //Convert the mouse coordinates to a locatoin on the map
                Location loc = myMap.ViewportPointToLocation(mousePosition);
                pttmp = loc;
                if (pt1 != null)
                {
                    DrawBoundary(pt1, pttmp);
                }
            }
            /*else
            {
                Point mousePosition = e.GetPosition(this);
                //Convert the mouse coordinates to a locatoin on the map
                Location loc = myMap.ViewportPointToLocation(mousePosition);
                string str = "0 1 point " + loc.Latitude + " " + loc.Longitude;
                tw.WriteLine(str); 
            }*/
        }
        void DrawBoundary(Location pt1, Location pt2)// draw the data boundary
        {
            tmpPolyline.Stroke = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Orange);
            tmpPolyline.StrokeThickness = 2;
            tmpPolyline.Opacity = 0.7;
          

            tmpPolyline.Locations = new LocationCollection() { 
            new Location(pt1.Latitude, pt1.Longitude), 
            new Location(pt1.Latitude,pt2.Longitude), 
            new Location(pt2.Latitude,pt2.Longitude),
            new Location(pt2.Latitude,pt1.Longitude),
            new Location(pt1.Latitude, pt1.Longitude)};
            myMap.Children.Remove(tmpPolyline);
            myMap.Children.Add(tmpPolyline);
        }

        void addArea(Location pt1, Location pt2) 
        {
            Polygon p=polygonMgr.addPolygon(pt1,pt2);
            if (p != null) { 
            if(ParamMgr.subscribeState==STATE.INTERSCT)
                movingObjMgr.subscribeIntersectNewPolygon(p.name,p.polygon);
            else if(ParamMgr.subscribeState==STATE.DIST)
                movingObjMgr.subscribeDistNewPolygon(p.name, p.polygon);
        }
            //string msg = String.Format("subscribing obj # {0}", PolygonMgr.polygonDict.Count);
            //this.textBlock1.Text = msg;
        }


        private void slider_NumValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ParamMgr.VehicleNumber = (int)e.NewValue;
            string msg = String.Format(" Vehicle Number: {0}", (int)e.NewValue);
            this.VehicleNum.Text = msg;
        }

        private void slider_SpeedValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ParamMgr.MovementIntervalInMS = 1000/(int)e.NewValue;
            if(movingObjMgr!=null && stoped==false)movingObjMgr.restartProcTraffic();
            string msg = String.Format(" Vehicle Speed: {0}", (int)e.NewValue);
            this.VehicleSpeed.Text = msg;
        }

        private void slider_SpeedValueChanged2(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ParamMgr.MovementIntervalInMS = 1000 / (int)e.NewValue;
            if (movingObjMgr != null && stoped == false) movingObjMgr.restartProcTraffic();
            string msg = String.Format(" Movement Speed: {0}", (int)e.NewValue);
            this.VehicleSpeed2.Text = msg;
        }

        private void slider_SpeedValueChanged3(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ParamMgr.MovementIntervalInMS = 1000 / (int)e.NewValue;
            if (movingObjMgr != null && stoped == false) movingObjMgr.restartProcTraffic();
            string msg = String.Format(" Movement Speed: {0}", (int)e.NewValue);
            this.VehicleSpeed2_.Text = msg;
        }

        private void slider_NumValueChanged1(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ParamMgr.VehicleNumber = (int)e.NewValue;
            string msg = String.Format(" Vehicle Number: {0}", (int)e.NewValue);
            this.VehicleNum1.Text = msg;
        }

        private void slider_SpeedValueChanged1(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ParamMgr.MovementIntervalInMS = 1000 / (int)e.NewValue;
            if (movingObjMgr != null && stoped ==false ) movingObjMgr.restartProcTraffic();
            string msg = String.Format(" Vehicle Speed: {0}", (int)e.NewValue);
            this.VehicleSpeed1.Text = msg;
        }
        private void slider_DistChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ParamMgr.ObsvDist = e.NewValue;
            string msg = String.Format(" Observe Distance: {0:F}", e.NewValue);
            this.ObserveDist.Text = msg;
        }
        private void slider_DistChanged1(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ParamMgr.ObsvDist = e.NewValue;
            string msg = String.Format(" Observe Distance: {0:F}", e.NewValue);
            this.ObserveDist1.Text = msg;
        }
        private void slider_DistConnChanged1(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ParamMgr.ObsvDist = e.NewValue;
            string msg = String.Format(" Observe Distance: {0:F}", e.NewValue);
            this.ObserveDist1_.Text = msg;
        }

        private void tabClick_Dist(object sender, MouseButtonEventArgs e)
        {
            updateSubscribeState(STATE.DIST);
        }

        private void tabClick_Intersect(object sender, MouseButtonEventArgs e)
        {
            updateSubscribeState(STATE.INTERSCT);
        }
        private void tabClick_RxRxDist(object sender, MouseButtonEventArgs e)
        {
            updateSubscribeState(STATE.RXRXDIST);
        }
        private void tabClick_RxRxDistConnection(object sender, MouseButtonEventArgs e)
        {
            updateSubscribeState(STATE.RXRXDISTCONN);
        }
        void updateSubscribeState(STATE newState)
        {
           // if (ParamMgr.subscribeState == newState) return;
            //else
            //{
                stoped = false;
                this.PauseResume.Content = "Pause";
                this.PauseResume1.Content = "Pause";
                this.PauseResume2.Content = "Pause";
                this.PauseResume3.Content = "Pause";
                this.VehicleNum.Text = " Vehicle Number: 1";
                this.VehicleSpeed.Text = " Vehicle Speed: 1";
                this.VehicleNum1.Text = " Vehicle Number: 1";
                this.VehicleSpeed1.Text = " Vehicle Speed: 1";
                this.ObserveDist.Text = " Observe Distance: 1";
                this.VehicleSpeed2.Text = " Movement Speed: 1";
                this.ObserveDist1.Text = "  Observe Distance: 1";
                this.RxRxObserverState.Text = "Observer Conditon:";

                this.RxRxObserverState_.Text = "Observer Conditon:";

                this.slider.Value = this.slider.Minimum;
                this.slider1.Value = this.slider1.Minimum;
                this.slider2.Value = this.slider2.Minimum;
                this.slider21.Value = this.slider21.Minimum;
                this.slider22.Value = this.slider22.Minimum;
                this.slider3.Value = this.slider3.Minimum;
                this.slider32.Value = this.slider32.Minimum;
                this.slider22_.Value = this.slider22_.Minimum;
                this.slider32_.Value = this.slider32_.Minimum;
                this.sliderRxRxId1.Value = this.sliderRxRxId1.Minimum;
                this.sliderRxRxId2.Value = this.sliderRxRxId2.Minimum;


                this.sliderRxRxConnId1.Value = this.sliderRxRxConnId1.Minimum;
                this.sliderRxRxConnId2.Value = this.sliderRxRxConnId2.Minimum;
                ParamMgr.MovementIntervalInMS = 1000;
                ParamMgr.ObsvDist = 1;
                if (newState == STATE.RXRXDIST || newState == STATE.RXRXDISTCONN)
                {
                    ParamMgr.VehicleNumber = 20;
                }
                else
                {
                    ParamMgr.VehicleNumber = 1;
                }
                ParamMgr.subscribeState = newState;

                if (movingObjMgr != null) movingObjMgr.restartProcTraffic();
                movingObjMgr.unsubscribeAll();
                polygonMgr.clearAllPolygons();
                
           // }

        }
       /* int RxRxObserverPairNum=0;
        private void RxObserverKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
               String s= this.RxRxDistTrackInputTextBox.Text;
               if (s != null)
               {
                   s.Trim();
                   String[] strs = s.Split(' ');
                   try{
                       int id1 = Convert.ToInt32(strs[0]);
                       int id2 = Convert.ToInt32(strs[strs.Length - 1]);
                       if ((id1 >= 0) && (id1 <= 19) && (id2 >= 0) && (id2 <= 19))
                       {
                           if (movingObjMgr.subscribeRxRxDist(id1, id2))
                           {

                               if(RxRxObserverPairNum%4==0){
                                string msg=this.RxRxObserverState.Text+"&#x0a;"+s;
                                this.RxRxObserverState.Text = msg;
                               }
                               else{
                                string msg = this.RxRxObserverState.Text + " | " + "("+id1.ToString()+","+id2.ToString()+")";
                                this.RxRxObserverState.Text = msg;
                               }
                               RxRxObserverPairNum++;
                           }
                           else
                           {
                               this.RxRxDistTrackInputTextBox.Text = "Input Err";
                           }
                           return;
                       }
                       else
                       {
                           this.RxRxDistTrackInputTextBox.Text = "Input id1,id2 range [0,19]";
                           return;
                       }
                       }
                   catch(Exception ex){

                   }
                   
               }
               this.RxRxDistTrackInputTextBox.Text = "InputFormat:id1;id2";
               return;
            }
        }*/
        bool stoped = false;
        private void click_StopResume(object sender, RoutedEventArgs e)
        {
            if (stoped == false)
            {

                movingObjMgr.stopTraffic();
                stoped = true;
                this.PauseResume.Content = "Resume";
                this.PauseResume1.Content = "Resume";
                this.PauseResume2.Content = "Resume";
                this.PauseResume3.Content = "Resume";
            }
            else
            {
                movingObjMgr.restartProcTraffic();
                stoped = false;
                this.PauseResume.Content = "Pause";
                this.PauseResume1.Content = "Pause";
                this.PauseResume2.Content = "Pause";
                this.PauseResume3.Content = "Pause";
            }
        }
        private void ClearAreaBtn_Click(object sender, RoutedEventArgs e)
        {
            movingObjMgr.unsubscribeAll();
            polygonMgr.clearAllPolygons();
        }
        
        private void AddRxRx_Click(object sender, RoutedEventArgs e)
        {
            if ((int)this.sliderRxRxId2.Value == (int)this.sliderRxRxId1.Value)
            {
               this.RxRxObserverState.Text = "Observer Condition:("
                + ((int)this.sliderRxRxId1.Value).ToString() + ","
                + ((int)this.sliderRxRxId2.Value).ToString() + ") Invalid!";
            }
            else{
                
           if( movingObjMgr.subscribeRxRxDist((int)this.sliderRxRxId1.Value,
               (int)this.sliderRxRxId2.Value)
            && movingObjMgr.subscribeRxRxDist((int)this.sliderRxRxId2.Value,
                (int)this.sliderRxRxId1.Value)){
            this.RxRxObserverState.Text = "Observer Condition:("
                + ((int)this.sliderRxRxId1.Value).ToString() + ","
                + ((int)this.sliderRxRxId2.Value).ToString() + ") ADD!";//}
            }
           else
           {
               this.RxRxObserverState.Text = "Observer Condition:WaitTraffic/Duplicate";
           }
            }

        }

        private void AddRxRxConn_Click(object sender, RoutedEventArgs e)
        {
            if ((int)this.sliderRxRxConnId2.Value == (int)this.sliderRxRxConnId1.Value)
            {
                this.RxRxObserverState_.Text = "Observer Condition:("
                 + ((int)this.sliderRxRxConnId1.Value).ToString() + ","
                 + ((int)this.sliderRxRxConnId2.Value).ToString() + ") Invalid!";
            }
            else
            {

                if (movingObjMgr.subscribeRxRxDistConnection((int)this.sliderRxRxConnId1.Value,
                    (int)this.sliderRxRxConnId2.Value)
                 && movingObjMgr.subscribeRxRxDistConnection((int)this.sliderRxRxConnId2.Value,
                     (int)this.sliderRxRxConnId1.Value))
                {
                    this.RxRxObserverState_.Text = "Observer Condition:("
                        + ((int)this.sliderRxRxConnId1.Value).ToString() + ","
                        + ((int)this.sliderRxRxConnId2.Value).ToString() + ") ADD!";//}
                }
                else
                {
                    this.RxRxObserverState_.Text = "Observer Condition:WaitTraffic/Duplicate";
                }
            }

        }

        private void slider_RxRxDistId(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.RxRxObserverState.Text = "Observers:("
    + ((int)this.sliderRxRxId1.Value).ToString() + ","
    + ((int)this.sliderRxRxId2.Value).ToString() + ") SELECTED..";
        }

        private void slider_RxRxDistConnId(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.RxRxObserverState_.Text = "Observers:("
    + ((int)this.sliderRxRxConnId1.Value).ToString() + ","
    + ((int)this.sliderRxRxConnId2.Value).ToString() + ") SELECTED..";
        }
    }
}

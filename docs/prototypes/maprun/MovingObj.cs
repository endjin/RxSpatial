using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Maps.MapControl.WPF;
using Microsoft.SqlServer.Types;
using System.Data.SqlTypes;

using System.Windows.Media;

using RxSpatial;

namespace MapRunRxSpatial
{
    class MovingObj
    {
        int id;
        
        ObjMovementHandler objMovementHandler; //visualize movement by pushpin
        private List<Observer> observers = new List<Observer>();// all other observers be invoked before locObserver
        private ObjMovementObserver objMovementObserver; //receive location feed to trigger visualized movement in objMovementHandler
        private int timestamp;
        RxGeography newLocation = new RxGeography();

        public MovingObj(int id, Map baseMap)
        {
            this.id = id;          
            this.objMovementHandler=new ObjMovementHandler(id,baseMap);
            this.objMovementObserver = new ObjMovementObserver(objMovementHandler);
            this.objMovementObserver.Subscribe(newLocation);
        }


        public void feedLocation(SqlGeography location)
        {
            newLocation.OnNext(location);
        }

#region ObserverListMgmtDomain
        public void addIntersectObserver(string name, List<SqlGeography> polygonList)
        {
            IntersctObserver intersectObserver = new IntersctObserver(name,polygonList,objMovementHandler);
            intersectObserver.Subscribe(newLocation);
            //Put the locObserver to the end so that every observer could update pushpin info before actually moved;
            objMovementObserver.OnCompleted();
            objMovementObserver.Subscribe(newLocation);

            observers.Add(intersectObserver);
        }

        public void delIntersectObserver(string name)
        {
            foreach (Observer observer in observers)
            {
                if (observer.Type.Equals(Observer.ObserverType.Intersect) && observer.Name.Equals(name))
                {
                    observer.OnCompleted();
                    observers.Remove(observer);
                    return;
                }
            }
        }
#endregion

        public void delObj()
        {
            foreach (Observer observer in observers)
            {
                observer.OnCompleted();
            }
            observers.Clear();
            this.objMovementObserver.OnCompleted();
        }
    }
}

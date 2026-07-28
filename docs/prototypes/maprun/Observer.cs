using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.SqlServer.Types;
using System.Data.SqlTypes;


using Microsoft.Maps.MapControl.WPF;
using System.Windows.Media;

namespace MapRunRxSpatial
{
    abstract class Observer:IObserver<SqlGeography>
    {
       public enum ObserverType{
            Intersect,
            Null,
        }
        private ObserverType type=ObserverType.Null;
        private string name="";
        protected void setObserverType(ObserverType type)
        {
            this.type=type;
        }

        public ObserverType Type
        { get { return this.type; } }

        protected void setName( string name)
        {
            this.name = name;
        }
        public string Name
        { get { return this.name; } }

        private IDisposable unsubscriber;
        public virtual void Subscribe(IObservable<SqlGeography> provider)
        {
            if (provider != null)
                unsubscriber = provider.Subscribe(this);
        }

        public virtual void OnCompleted()
        {
            this.Unsubscribe();
        }


        public virtual void OnError(Exception e)
        {

        }

        public virtual void OnNext(SqlGeography loc)
        {
        }

        public virtual void Unsubscribe()
        {
            unsubscriber.Dispose();
        }
    }
}

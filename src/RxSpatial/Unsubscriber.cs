using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.SqlServer.Types;
using RxSpatial.Lib;
namespace RxSpatial
{
    public class Unsubscriber<T> : IDisposable
    {
        public Unsubscriber(IContainer<IObserver<T>> observers, IObserver<T> observer)
        {
            this._observers = observers;
            this._observer = observer;
        }

        public void Dispose()
        {
            if (_observer != null && _observers.Contains(_observer))
                _observers.Remove(_observer);
        }

        private IContainer<IObserver<T>> _observers;
        private IObserver<T> _observer;
    }
}

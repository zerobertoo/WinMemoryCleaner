using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace WinMemoryCleaner
{
    /// <summary>
    /// A base class for objects of which the properties must be observable.
    /// </summary>
    /// <seealso cref="INotifyPropertyChanged" />
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        #region Events

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Methods

        /// <summary>
        /// Called when [property changed].
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        public void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (PropertyChanged is not null)
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Raises the PropertyChanged event if needed.
        /// </summary>
        /// <typeparam name="T">The type of the property that
        /// changed.</typeparam>
        /// <param name="expression">An expression identifying the property
        /// that changed.</param>
        public void RaisePropertyChanged<T>(Expression<Func<T>> expression)
        {
            var handler = PropertyChanged;

            if (handler is not null && expression is not null)
            {
                var body = expression.Body as MemberExpression;

                if (body is not null)
                {
                    var property = body.Member as PropertyInfo;

                    if (property is not null)
                    {
                        var propertyName = property.Name;

                        if (!string.IsNullOrEmpty(propertyName))
                            RaisePropertyChanged(propertyName);
                    }
                }
            }
        }

        #endregion
    }
}

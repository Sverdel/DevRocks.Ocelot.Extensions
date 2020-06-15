using System;
using System.Linq.Expressions;

namespace DevRocks.Ocelot.Grpc.Internal
{
    public static class Factory<T>
        where T : new()
    {
        private static readonly Func<T> _createInstanceFunc = Expression.Lambda<Func<T>>(Expression.New(typeof(T))).Compile();

        public static T CreateInstance() => _createInstanceFunc();
    }
}

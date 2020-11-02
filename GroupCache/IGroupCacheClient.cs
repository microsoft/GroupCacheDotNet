using System;
using System.IO;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace GroupCache
{
    public interface IGroupCacheClient : IDisposable
    {
        Task GetAsync(string group, string key, Stream sink, ICacheControl cacheControl, CancellationToken ct);

        PeerEndpoint Endpoint { get; }

        /// <summary>
        /// This is true if this client is not making a request to a remote machine but simply read from local cache
        /// </summary>
        bool IsLocal { get; }
    }

    [Serializable]
    public class InternalServerErrorException : Exception
    {
        public InternalServerErrorException(string message) : base(message)
        {
        }

        public InternalServerErrorException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected InternalServerErrorException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

    [Serializable]
    public class ConnectFailureException : Exception
    {
        public ConnectFailureException()
        {
        }
        public ConnectFailureException(string message) : base(message)
        {
        }

        public ConnectFailureException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected ConnectFailureException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

    [Serializable]
    public class ServerBusyException : Exception
    {
        public ServerBusyException()
        {
        }

        public ServerBusyException(string message) : base(message)
        {
        }
    }

    [Serializable]
    public class GroupNotFoundException : Exception
    {
        public GroupNotFoundException()
        {
        }
        public GroupNotFoundException(string message) : base(message)
        {
        }

        public GroupNotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected GroupNotFoundException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

}

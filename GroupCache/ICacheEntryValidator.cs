using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GroupCache
{
    public class CacheEntryValidationFailedException : Exception
    {
        public CacheEntryValidationFailedException() : base()
        {
        }

        public CacheEntryValidationFailedException(string message) : base(message)
        {
        }

        public CacheEntryValidationFailedException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public abstract class ValidationStream : Stream
    {
        public abstract Task ValidateAsync(CancellationToken ct);
    }

    public interface ICacheEntryValidator
    {
        ValidationStream ValidateEntryPassThrough(string key, Stream streamPayload);
    }
}
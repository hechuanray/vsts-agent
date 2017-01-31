using System;

namespace Microsoft.VisualStudio.Services.DistributedTask.Expressions
{
    public static class ArgUtil
    {
        public static void NotNull(object value, string name)
        {
            if (object.ReferenceEquals(value, null))
            {
                throw new ArgumentNullException(name);
            }
        }

        public static void NotNullOrEmpty(string value, string name)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentNullException(name);
            }
        }

        public static void NotEmpty(Guid value, string name)
        {
            if (value == Guid.Empty)
            {
                throw new ArgumentNullException(name);
            }
        }

        public static void Null(object value, string name)
        {
            if (!object.ReferenceEquals(value, null))
            {
                throw new ArgumentException(message: $"{name} should be null.", paramName: name);
            }
        }
    }
}
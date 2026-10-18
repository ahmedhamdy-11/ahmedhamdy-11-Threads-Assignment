namespace OrderProcessing.Shared.Exceptions;

public class CustomerNotFoundException : Exception
{
    public CustomerNotFoundException(string message) : base(message) { }
}

public class InvalidCouponException : Exception
{
    public InvalidCouponException(string message) : base(message) { }
}

public class ShippingProviderUnavailableException : Exception
{
    public ShippingProviderUnavailableException(string message) : base(message) { }
}

public class DatabaseWriteException : Exception
{
    public DatabaseWriteException(string message) : base(message) { }
}

public class SmtpUnavailableException : Exception
{
    public SmtpUnavailableException(string message) : base(message) { }
}

public class DataFileCorruptedException : Exception
{
    public DataFileCorruptedException(string fileName, Exception? inner = null) 
        : base($"Data file '{fileName}' is corrupted.", inner) { }
}

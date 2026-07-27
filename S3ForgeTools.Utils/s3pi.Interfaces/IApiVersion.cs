namespace s3pi.Interfaces;

public interface IApiVersion
{
	int RequestedApiVersion { get; }

	int RecommendedApiVersion { get; }
}

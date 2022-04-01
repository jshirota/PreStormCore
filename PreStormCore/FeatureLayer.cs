using System.Net;

namespace PreStormCore;

public interface ILayer<T> where T : Feature
{
    string Url { get; }
    string? Token { get; }
    Task<LayerInfo> GetLayerInfo();
    IEnumerable<T> Download(params int[] objectIds);
    IEnumerable<T> Download(string? whereClause = null, string? extraParameters = null, bool keepQuerying = false, int degreeOfParallelism = 1, CancellationToken? cancellationToken = null);
    IEnumerable<T> Download(GeometryBase? geometry, SpatialRel spatialRel, string? whereClause = null, string? extraParameters = null, bool keepQuerying = false, int degreeOfParallelism = 1, CancellationToken? cancellationToken = null);
    IAsyncEnumerable<T> DownloadAsync(params int[] objectIds);
    IAsyncEnumerable<T> DownloadAsync(string? whereClause = null, string? extraParameters = null, bool keepQuerying = false);
    IAsyncEnumerable<T> DownloadAsync(GeometryBase? geometry, SpatialRel spatialRel, string? whereClause = null, string? extraParameters = null, bool keepQuerying = false);
}

public class Layer<T> : ILayer<T> where T : Feature
{
    private readonly int maxRecordCount = 10;
    private LayerInfo? layerInfo = null;

    private DateTime expiry;
    private readonly Func<Task> updateTokenAsync;

    public string Url { get; }
    public string? Token { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Layer{T}"/> class.
    /// </summary>
    /// <param name="url">Layer url (ends with the layer id).</param>
    /// <param name="token">Token.</param>
    public Layer(string url, string? token = null)
    {
        Url = url;
        updateTokenAsync = () => Task.CompletedTask;
        Token = token;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Layer{T}"/> class.
    /// </summary>
    /// <param name="url">Layer url (ends with the layer id).</param>
    /// <param name="user">User.</param>
    /// <param name="password">Password.</param>
    /// <param name="tokenUrl">Token url.</param>
    public Layer(string url, string user, string password, string tokenUrl = "https://www.arcgis.com/sharing/rest/generateToken")
    {
        Url = url;

        updateTokenAsync = async () =>
        {
            if (expiry.Subtract(DateTime.UtcNow).TotalMinutes > 1)
                return;

            var tokenInfo = await Esri.GetTokenInfo(tokenUrl, user, password, 60);
            expiry = Esri.BaseTime.AddMilliseconds(tokenInfo.expires);
            Token = tokenInfo.token;
        };
    }

    public async Task<LayerInfo> GetLayerInfo()
    {
        await updateTokenAsync();

        layerInfo = await Esri.GetLayer(Url, Token);

        return layerInfo;
    }

    public IEnumerable<T> Download(params int[] objectIds)
    {
        updateTokenAsync().Wait();

        var layerInfo = this.layerInfo ?? GetLayerInfo().Result;

        return Download(objectIds, null, null, layerInfo.maxRecordCount ?? maxRecordCount, 1, null);
    }

    public IEnumerable<T> Download(string? whereClause = null, string? extraParameters = null, bool keepQuerying = false, int degreeOfParallelism = 1, CancellationToken? cancellationToken = null)
    {
        updateTokenAsync().Wait();

        var layerInfo = this.layerInfo ?? GetLayerInfo().Result;

        var featureSet = Esri.GetFeatureSet(Url, Token, typeof(T).HasGeometry(), layerInfo.hasZ, whereClause, extraParameters, null).Result;

        foreach (var g in featureSet.features)
            yield return g.ToFeature<T>(layerInfo);

        var objectIds = featureSet.features.Select(g => g.attributes[layerInfo.GetObjectIdFieldName()].GetInt32()).ToArray();

        if (!keepQuerying || objectIds.Length == 0)
            yield break;

        var remainingObjectIds = Esri.GetOIDSet(Url, Token, whereClause, extraParameters).Result.objectIds.Except(objectIds);

        foreach (var f in Download(remainingObjectIds, whereClause, extraParameters, layerInfo.maxRecordCount ?? objectIds.Length, degreeOfParallelism, cancellationToken))
            yield return f;
    }

    public IEnumerable<T> Download(GeometryBase? geometry, SpatialRel spatialRel, string? whereClause = null, string? extraParameters = null, bool keepQuerying = false, int degreeOfParallelism = 1, CancellationToken? cancellationToken = null)
    {
        updateTokenAsync().Wait();

        var spatialFilter = $"geometry={WebUtility.UrlEncode(geometry?.ToJson())}&geometryType={Layer<T>.ToGeometryType(geometry)}&spatialRel=esriSpatialRel{spatialRel}";

        return Download(whereClause, string.IsNullOrEmpty(extraParameters) ? spatialFilter : (extraParameters + "&" + spatialFilter), keepQuerying, degreeOfParallelism, cancellationToken);
    }

    private IEnumerable<T> Download(IEnumerable<int> objectIds, string? whereClause, string? extraParameters, int batchSize, int degreeOfParallelism, CancellationToken? cancellationToken)
    {
        updateTokenAsync().Wait();

        var layerInfo = this.layerInfo ?? GetLayerInfo().Result;

        var returnGeometry = typeof(T).HasGeometry();

        return objectIds.Chunk(batchSize)
            .AsParallel()
            .AsOrdered()
            .WithDegreeOfParallelism(degreeOfParallelism < 1 ? 1 : degreeOfParallelism)
            .WithCancellation(cancellationToken ?? CancellationToken.None)
            .SelectMany(ids => Esri.GetFeatureSet(Url, Token, returnGeometry, layerInfo.hasZ, whereClause, extraParameters, ids).Result.features
                .Select(g => g.ToFeature<T>(layerInfo)));
    }

    public async IAsyncEnumerable<T> DownloadAsync(params int[] objectIds)
    {
        await updateTokenAsync();

        var layerInfo = await GetLayerInfo();

        await foreach (var feature in DownloadAsync(objectIds, null, null, layerInfo.maxRecordCount ?? maxRecordCount))
            yield return feature;
    }

    public async IAsyncEnumerable<T> DownloadAsync(string? whereClause = null, string? extraParameters = null, bool keepQuerying = false)
    {
        await updateTokenAsync();

        var layerInfo = this.layerInfo ?? await GetLayerInfo();

        var featureSet = await Esri.GetFeatureSet(Url, Token, typeof(T).HasGeometry(), layerInfo.hasZ, whereClause, extraParameters, null);

        foreach (var graphic in featureSet.features)
            yield return graphic.ToFeature<T>(layerInfo);

        var objectIds = featureSet.features.Select(g => g.attributes[layerInfo.GetObjectIdFieldName()].GetInt32()).ToArray();

        if (!keepQuerying || objectIds.Length == 0)
            yield break;

        var oidSet = await Esri.GetOIDSet(Url, Token, whereClause, extraParameters);

        var remainingObjectIds = oidSet.objectIds.Except(objectIds);

        await foreach (var feature in DownloadAsync(remainingObjectIds, whereClause, extraParameters, layerInfo.maxRecordCount ?? objectIds.Length))
            yield return feature;
    }

    public async IAsyncEnumerable<T> DownloadAsync(GeometryBase? geometry, SpatialRel spatialRel, string? whereClause = null, string? extraParameters = null, bool keepQuerying = false)
    {
        await updateTokenAsync();

        var spatialFilter = $"geometry={WebUtility.UrlEncode(geometry?.ToJson())}&geometryType={Layer<T>.ToGeometryType(geometry)}&spatialRel=esriSpatialRel{spatialRel}";

        await foreach (var feature in DownloadAsync(whereClause, string.IsNullOrEmpty(extraParameters) ? spatialFilter : (extraParameters + "&" + spatialFilter), keepQuerying))
            yield return feature;
    }

    private async IAsyncEnumerable<T> DownloadAsync(IEnumerable<int> objectIds, string? whereClause, string? extraParameters, int batchSize)
    {
        await updateTokenAsync();

        var layerInfo = this.layerInfo ?? await GetLayerInfo();

        var returnGeometry = typeof(T).HasGeometry();

        foreach (var ids in objectIds.Chunk(batchSize))
        {
            var featureSet = await Esri.GetFeatureSet(Url, Token, returnGeometry, layerInfo.hasZ, whereClause, extraParameters, ids);

            foreach (var graphic in featureSet.features)
                yield return graphic.ToFeature<T>(layerInfo);
        }
    }

    private static GeometryType ToGeometryType(GeometryBase? geometry)
    {
        return geometry switch
        {
            Point => GeometryType.esriGeometryPoint,
            Multipoint => GeometryType.esriGeometryMultipoint,
            Polyline => GeometryType.esriGeometryPolyline,
            Polygon => GeometryType.esriGeometryPolygon,
            Envelope => GeometryType.esriGeometryEnvelope,
            _ => throw new ArgumentException("This geometry type is not supported.", nameof(geometry))
        };
    }
}

public interface ICreate<T> : ILayer<T> where T : Feature { }
public interface IUpdate<T> : ILayer<T> where T : Feature { }
public interface IDelete<T> : ILayer<T> where T : Feature { }
public interface ICreateOrUpdate<T> : ICreate<T>, IUpdate<T> where T : Feature { }
public interface ICreateOrDelete<T> : ICreate<T>, IDelete<T> where T : Feature { }
public interface IUpdateOrDelete<T> : IUpdate<T>, IDelete<T> where T : Feature { }
public interface IFeatureLayer<T> : ICreate<T>, IUpdate<T>, IDelete<T> where T : Feature { }

public class FeatureLayer<T> : Layer<T>, IFeatureLayer<T> where T : Feature
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FeatureLayer{T}"/> class.
    /// </summary>
    /// <param name="url">Layer url (ends with the layer id).</param>
    /// <param name="token">Token.</param>
    public FeatureLayer(string url, string? token = null)
        : base(url, token)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FeatureLayer{T}"/> class.
    /// </summary>
    /// <param name="url">Layer url (ends with the layer id).</param>
    /// <param name="user">User.</param>
    /// <param name="password">Password.</param>
    /// <param name="tokenUrl">Token url.</param>
    public FeatureLayer(string url, string user, string password, string tokenUrl = "https://www.arcgis.com/sharing/rest/generateToken")
        : base(url, user, password, tokenUrl)
    {
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace PreStormCore
{
    public interface ILayer<T> where T : Feature
    {
        string Url { get; }
        string? Token { get; }
        LayerInfo LayerInfo { get; }
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
        private readonly Token? token;
        public string? Token => token?.ToString();
        public string Url { get; }
        public LayerInfo LayerInfo { get; }

        public Layer(string url, string? token = null)
        {
            this.token = token is null ? null : new Token(token);
            Url = url;
            LayerInfo = Esri.GetLayer(url, Token).Result;
        }

        public Layer(string url, string userName, string password)
        {
            this.token = new Token(url, userName, password);
            Url = url;
            LayerInfo = Esri.GetLayer(url, Token).Result;
        }

        public IEnumerable<T> Download(params int[] objectIds)
        {
            return Download(objectIds, null, null, LayerInfo.maxRecordCount ?? maxRecordCount, 1, null);
        }

        public IEnumerable<T> Download(string? whereClause = null, string? extraParameters = null, bool keepQuerying = false, int degreeOfParallelism = 1, CancellationToken? cancellationToken = null)
        {
            var featureSet = Esri.GetFeatureSet(Url, Token, typeof(T).HasGeometry(), LayerInfo.hasZ, whereClause, extraParameters, null).Result;

            foreach (var g in featureSet.features)
                yield return g.ToFeature<T>(LayerInfo);

            var objectIds = featureSet.features.Select(g => g.attributes[LayerInfo.GetObjectIdFieldName()].GetInt32()).ToArray();

            if (!keepQuerying || objectIds.Length == 0)
                yield break;

            var remainingObjectIds = Esri.GetOIDSet(Url, Token, whereClause, extraParameters).Result.objectIds.Except(objectIds);

            foreach (var f in Download(remainingObjectIds, whereClause, extraParameters, LayerInfo.maxRecordCount ?? objectIds.Length, degreeOfParallelism, cancellationToken))
                yield return f;
        }

        public IEnumerable<T> Download(GeometryBase? geometry, SpatialRel spatialRel, string? whereClause = null, string? extraParameters = null, bool keepQuerying = false, int degreeOfParallelism = 1, CancellationToken? cancellationToken = null)
        {
            var spatialFilter = $"geometry={WebUtility.UrlEncode(geometry?.ToJson())}&geometryType={Layer<T>.ToGeometryType(geometry)}&spatialRel=esriSpatialRel{spatialRel}";

            return Download(whereClause, string.IsNullOrEmpty(extraParameters) ? spatialFilter : (extraParameters + "&" + spatialFilter), keepQuerying, degreeOfParallelism, cancellationToken);
        }

        private IEnumerable<T> Download(IEnumerable<int> objectIds, string? whereClause, string? extraParameters, int batchSize, int degreeOfParallelism, CancellationToken? cancellationToken)
        {
            var returnGeometry = typeof(T).HasGeometry();

            return objectIds.Partition(batchSize)
                .AsParallel()
                .AsOrdered()
                .WithDegreeOfParallelism(degreeOfParallelism < 1 ? 1 : degreeOfParallelism)
                .WithCancellation(cancellationToken ?? CancellationToken.None)
                .SelectMany(ids => Esri.GetFeatureSet(Url, Token, returnGeometry, LayerInfo.hasZ, whereClause, extraParameters, ids).Result.features
                    .Select(g => g.ToFeature<T>(LayerInfo)));
        }

        public async IAsyncEnumerable<T> DownloadAsync(params int[] objectIds)
        {
            await foreach (var feature in DownloadAsync(objectIds, null, null, LayerInfo.maxRecordCount ?? maxRecordCount))
                yield return feature;
        }

        public async IAsyncEnumerable<T> DownloadAsync(string? whereClause = null, string? extraParameters = null, bool keepQuerying = false)
        {
            var featureSet = await Esri.GetFeatureSet(Url, Token, typeof(T).HasGeometry(), LayerInfo.hasZ, whereClause, extraParameters, null);

            foreach (var graphic in featureSet.features)
                yield return graphic.ToFeature<T>(LayerInfo);

            var objectIds = featureSet.features.Select(g => g.attributes[LayerInfo.GetObjectIdFieldName()].GetInt32()).ToArray();

            if (!keepQuerying || objectIds.Length == 0)
                yield break;

            var oidSet = await Esri.GetOIDSet(Url, Token, whereClause, extraParameters);

            var remainingObjectIds = oidSet.objectIds.Except(objectIds);

            await foreach (var feature in DownloadAsync(remainingObjectIds, whereClause, extraParameters, LayerInfo.maxRecordCount ?? objectIds.Length))
                yield return feature;
        }

        public async IAsyncEnumerable<T> DownloadAsync(GeometryBase? geometry, SpatialRel spatialRel, string? whereClause = null, string? extraParameters = null, bool keepQuerying = false)
        {
            var spatialFilter = $"geometry={WebUtility.UrlEncode(geometry?.ToJson())}&geometryType={Layer<T>.ToGeometryType(geometry)}&spatialRel=esriSpatialRel{spatialRel}";

            await foreach (var feature in DownloadAsync(whereClause, string.IsNullOrEmpty(extraParameters) ? spatialFilter : (extraParameters + "&" + spatialFilter), keepQuerying))
                yield return feature;
        }

        private async IAsyncEnumerable<T> DownloadAsync(IEnumerable<int> objectIds, string? whereClause, string? extraParameters, int batchSize)
        {
            var returnGeometry = typeof(T).HasGeometry();

            foreach (var ids in objectIds.Partition(batchSize))
            {
                var featureSet = await Esri.GetFeatureSet(Url, Token, returnGeometry, LayerInfo.hasZ, whereClause, extraParameters, ids);

                foreach (var graphic in featureSet.features)
                    yield return graphic.ToFeature<T>(LayerInfo);
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
        public FeatureLayer(string url, string? token = null) : base(url, token)
        {
        }

        public FeatureLayer(string url, string userName, string password) : base(url, userName, password)
        {
        }
    }
}

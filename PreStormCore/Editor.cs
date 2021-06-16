using System;
using System.Linq;
using System.Threading.Tasks;

namespace PreStormCore
{
    public static class Editor
    {
        public static async Task<EditResult<T>> InsertAsync<T>(this ICreate<T> layer, params T[] features) where T : Feature
            => await ApplyEdits(layer, featuresToAdd: features);

        public static async Task<EditResult<T>> UpdateAsync<T>(this IUpdate<T> layer, params T[] features) where T : Feature
            => await ApplyEdits(layer, featuresToUpdate: features);

        public static async Task<EditResult<T>> DeleteAsync<T>(this IDelete<T> layer, params T[] features) where T : Feature
            => await ApplyEdits(layer, featuresToDelete: features);

        public static async Task<EditResult<T>> DeleteAsync<T>(this IDelete<T> layer, string whereClause) where T : Feature
        {
            var editResultSet = await Esri.Delete(layer.Url, layer.Token, whereClause);

            return new EditResult<T>(editResultSet, layer);
        }

        public static async Task<EditResult<T>> ApplyEditsAsync<T>(this IFeatureLayer<T> layer, T[]? featuresToAdd = null, T[]? featuresToUpdate = null, T[]? featuresToDelete = null) where T : Feature
            => await ApplyEditsAsync(layer, featuresToAdd, featuresToUpdate, featuresToDelete);

        private static async Task<EditResult<T>> ApplyEdits<T>(this ILayer<T> layer, T[]? featuresToAdd = null, T[]? featuresToUpdate = null, T[]? featuresToDelete = null) where T : Feature
        {
            var adds = featuresToAdd?.Select(f => f.ToGraphic(layer.LayerInfo, false)).ToArray();
            var updates = featuresToUpdate?.Select(f => f.ToGraphic(layer.LayerInfo, true)).Where(o => o is not null).ToArray();
            var deletes = featuresToDelete?.Select(f => f.OID).ToArray();

            var editResultSet = await Esri.ApplyEdits(layer.Url, layer.Token, adds, updates, deletes);

            if (editResultSet.error is null)
            {
                foreach (var f in featuresToUpdate ?? Array.Empty<T>())
                {
                    f.IsDirty = false;
                }

                foreach (var f in featuresToDelete ?? Array.Empty<T>())
                {
                    f.OID = -1;
                    f.IsDirty = false;
                }
            }

            return new EditResult<T>(editResultSet, layer);
        }
    }
}

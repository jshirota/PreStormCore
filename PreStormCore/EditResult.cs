namespace PreStormCore;

public class EditResult<T> where T : Feature
{
    private readonly Lazy<T[]> insertedFeatures;
    public T[] InsertedFeatures => insertedFeatures.Value;
    public EditResultSet Raw { get; }
    public bool Success => Raw.error is null;

    internal EditResult(EditResultSet editResultSet, ILayer<T> featureLayer)
    {
        this.Raw = editResultSet;
        this.insertedFeatures = new Lazy<T[]>(()
            => featureLayer.Download(editResultSet.addResults.Where(x => x.objectId.HasValue).Select(r => r.objectId!.Value).ToArray()).ToArray());
    }
}

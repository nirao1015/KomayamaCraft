public interface IWorkplaceTarget
{
    bool CanAcceptItem(DraggableItemController item);
    void OnItemDropped(DraggableItemController item);
}

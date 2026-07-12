using SmasKunovice.Avalonia.Models.ConflictResolution;

namespace SmasKunovice.Avalonia.Tests.ConflictResolution;

[TestFixture]
public class ConflictRepositoryTests : TestBase
{
    private ConflictRepository _repository;

    [SetUp]
    public void SetUp()
    {
        _repository = new ConflictRepository();
    }

    [Test]
    public void UpdateConflict_NewConflict_ReturnsAdded()
    {
        var result = _repository.UpdateConflict("uas1", ConflictType.DroneAboveLimit, ConflictLevel.Warning);
        Assert.That(result, Is.EqualTo(ConflictUpdateResult.Added));
    }

    [Test]
    public void UpdateConflict_ExistingConflict_DifferentLevel_ReturnsModified()
    {
        _repository.UpdateConflict("uas1", ConflictType.DroneAboveLimit, ConflictLevel.Warning);
        var result = _repository.UpdateConflict("uas1", ConflictType.DroneAboveLimit, ConflictLevel.Alarm);
        Assert.That(result, Is.EqualTo(ConflictUpdateResult.Modified));
    }

    [Test]
    public void UpdateConflict_ExistingConflict_SameLevel_ReturnsUnchanged()
    {
        _repository.UpdateConflict("uas1", ConflictType.DroneAboveLimit, ConflictLevel.Warning);
        var result = _repository.UpdateConflict("uas1", ConflictType.DroneAboveLimit, ConflictLevel.Warning);
        Assert.That(result, Is.EqualTo(ConflictUpdateResult.Unchanged));
    }

    [Test]
    public void UpdateConflict_ExistingConflict_LevelNone_ReturnsRemoved()
    {
        _repository.UpdateConflict("uas1", ConflictType.DroneAboveLimit, ConflictLevel.Warning);
        var result = _repository.UpdateConflict("uas1", ConflictType.DroneAboveLimit, ConflictLevel.None);
        Assert.That(result, Is.EqualTo(ConflictUpdateResult.Removed));
    }

    [Test]
    public void UpdateConflict_NonExistent_LevelNone_ReturnsUnchanged()
    {
        var result = _repository.UpdateConflict("uas1", ConflictType.DroneAboveLimit, ConflictLevel.None);
        Assert.That(result, Is.EqualTo(ConflictUpdateResult.Unchanged));
    }

    [Test]
    public void UpdateConflict_NewTypeForExistingUas_ReturnsAdded()
    {
        _repository.UpdateConflict("uas1", ConflictType.DroneAboveLimit, ConflictLevel.Warning);
        var result = _repository.UpdateConflict("uas1", ConflictType.RpaPresence, ConflictLevel.Alarm);
        Assert.That(result, Is.EqualTo(ConflictUpdateResult.Added));
    }

    [Test]
    public void UpdateConflicts_BatchUpdate_ReturnsCorrectResults()
    {
        _repository.UpdateConflict("uas1", ConflictType.DroneAboveLimit, ConflictLevel.Warning); // Existing
        
        var uasIds = new[] { "uas1", "uas2", "uas3" };
        var results = _repository.UpdateConflicts(uasIds, ConflictType.DroneAboveLimit, ConflictLevel.Alarm);

        Assert.Multiple(() =>
        {
            Assert.That(results.Added, Is.EquivalentTo(new[] { "uas2", "uas3" }));
            Assert.That(results.Modified, Is.EquivalentTo(new[] { "uas1" }));
            Assert.That(results.Unchanged, Is.Empty);
            Assert.That(results.Removed, Is.Empty);
            Assert.That(results.HasChanges, Is.True);
        });
    }

    [Test]
    public void UpdateConflicts_MixedResults_ReturnsCorrectSets()
    {
        _repository.UpdateConflict("uas1", ConflictType.DroneAboveLimit, ConflictLevel.Warning);
        _repository.UpdateConflict("uas2", ConflictType.DroneAboveLimit, ConflictLevel.Alarm);
        
        var uasIds = new[] { "uas1", "uas2", "uas3" };
        var results = _repository.UpdateConflicts(uasIds, ConflictType.DroneAboveLimit, ConflictLevel.Alarm);
        
        Assert.Multiple(() =>
        {
            Assert.That(results.Added, Is.EquivalentTo(new[] { "uas3" }));
            Assert.That(results.Modified, Is.EquivalentTo(new[] { "uas1" }));
            Assert.That(results.Unchanged, Is.EquivalentTo(new[] { "uas2" }));
            Assert.That(results.Removed, Is.Empty);
        });
    }

    [Test]
    public void UpdateConflicts_RemovingConflicts_ReturnsCorrectSets()
    {
        _repository.UpdateConflict("uas1", ConflictType.DroneAboveLimit, ConflictLevel.Warning);
        _repository.UpdateConflict("uas2", ConflictType.DroneAboveLimit, ConflictLevel.Warning);
        
        var results = _repository.UpdateConflicts(new[] { "uas1", "uas2", "uas3" }, ConflictType.DroneAboveLimit, ConflictLevel.None);
        
        Assert.Multiple(() =>
        {
            Assert.That(results.Removed, Is.EquivalentTo(new[] { "uas1", "uas2" }));
            Assert.That(results.Unchanged, Is.EquivalentTo(new[] { "uas3" }));
            Assert.That(results.Added, Is.Empty);
            Assert.That(results.Modified, Is.Empty);
            Assert.That(results.HasChanges, Is.True);
        });
    }

    [Test]
    public void RemoveConflict_ExistingConflict_RemovesIt()
    {
        _repository.UpdateConflict("uas1", ConflictType.DroneAboveLimit, ConflictLevel.Warning);
        _repository.RemoveConflict("uas1", ConflictType.DroneAboveLimit);
        
        // Verifying by trying to remove it again via UpdateConflict
        var result = _repository.UpdateConflict("uas1", ConflictType.DroneAboveLimit, ConflictLevel.None);
        Assert.That(result, Is.EqualTo(ConflictUpdateResult.Unchanged));
        
        // Verifying it's "Added" if we add it again
        var addResult = _repository.UpdateConflict("uas1", ConflictType.DroneAboveLimit, ConflictLevel.Warning);
        Assert.That(addResult, Is.EqualTo(ConflictUpdateResult.Added));
    }

    [Test]
    public void RemoveById_ExistingUas_RemovesAllConflicts()
    {
        _repository.UpdateConflict("uas1", ConflictType.DroneAboveLimit, ConflictLevel.Warning);
        _repository.UpdateConflict("uas1", ConflictType.RpaPresence, ConflictLevel.Alarm);
        
        var removed = _repository.RemoveById("uas1");
        Assert.That(removed, Is.True);

        // Verifying it's "Added" if we add one back
        var result = _repository.UpdateConflict("uas1", ConflictType.DroneAboveLimit, ConflictLevel.Warning);
        Assert.That(result, Is.EqualTo(ConflictUpdateResult.Added));
    }

    [Test]
    public void RemoveById_NonExistentUas_ReturnsFalse()
    {
        var removed = _repository.RemoveById("nonexistent");
        Assert.That(removed, Is.False);
    }
}
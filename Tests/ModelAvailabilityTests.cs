using Xunit;

namespace FortniteEmotes;

public class ModelAvailabilityTests
{
    private const string Model = "characters/kolka/fortnite_dance.vmdl";

    [Fact]
    public void UnknownModelIsDeniedEvenIfMounted()
    {
        var models = new ModelAvailability();
        Assert.False(models.CanUse(Model, _ => true));
    }

    [Fact]
    public void DownloadedButUnmountedModelIsNeverRegistered()
    {
        var models = new ModelAvailability();
        var resources = new List<string>();
        Assert.False(models.Register(Model, _ => false, resources.Add));
        Assert.Empty(resources);
        Assert.False(models.CanUse(Model, _ => false));
        // Mounting it later still requires a new resource precache.
        Assert.False(models.CanUse(Model, _ => true));
    }

    [Fact]
    public void MountedAndRegisteredModelIsAllowed()
    {
        var models = new ModelAvailability();
        var resources = new List<string>();
        Assert.True(models.Register(Model, _ => true, resources.Add));
        Assert.Equal(new[] { Model }, resources);
        Assert.True(models.CanUse(Model, _ => true));
    }

    [Fact]
    public void UnmountBetweenRequestAndDeferredSetModelIsDenied()
    {
        bool mounted = true;
        var models = new ModelAvailability();
        models.Register(Model, _ => mounted, _ => { });
        Assert.True(models.CanUse(Model, _ => mounted));
        mounted = false;
        Assert.False(models.CanUse(Model, _ => mounted));
    }

    [Fact]
    public void MapChangeAndHotReloadDoNotReuseReadiness()
    {
        var models = new ModelAvailability();
        models.Register(Model, _ => true, _ => { });
        models.Clear();
        Assert.False(models.CanUse(Model, _ => true));
        Assert.False(new ModelAvailability().CanUse(Model, _ => true));
    }

    [Fact]
    public void RegistrationFailureDoesNotPermitModel()
    {
        var models = new ModelAvailability();
        Assert.Throws<InvalidOperationException>(() =>
            models.Register(Model, _ => true, _ => throw new InvalidOperationException()));
        Assert.False(models.CanUse(Model, _ => true));
    }

    [Fact]
    public void ModelCannotBeUsedWhileRegistrationIsStillRunning()
    {
        var models = new ModelAvailability();
        models.Register(Model, _ => true, _ => Assert.False(models.CanUse(Model, _ => true)));
        Assert.True(models.CanUse(Model, _ => true));
    }

    [Fact]
    public void SharedModelIsRegisteredOnce()
    {
        var models = new ModelAvailability();
        int registrations = 0;
        for (int i = 0; i < 80; i++)
            Assert.True(models.Register(Model, _ => true, _ => registrations++));
        Assert.Equal(1, registrations);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("../characters/dance.vmdl")]
    [InlineData("characters/../dance.vmdl")]
    [InlineData("characters//dance.vmdl")]
    [InlineData("/characters/dance.vmdl")]
    [InlineData("C:/characters/dance.vmdl")]
    [InlineData("characters\\dance.vmdl")]
    [InlineData("characters/dance.vmdl_c")]
    [InlineData("characters/dance.vmdl\0")]
    public void InvalidPathsNeverReachResourceLookup(string? model)
    {
        var models = new ModelAvailability();
        bool Lookup(string _) => throw new Exception("Invalid path reached the engine");
        Assert.False(ModelAvailability.IsValidPath(model));
        Assert.False(models.Register(model!, Lookup, _ => throw new Exception()));
        Assert.False(models.CanUse(model!, Lookup));
    }
}

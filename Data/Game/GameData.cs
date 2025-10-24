﻿using CS2GameHelper.Data.Entity;
using CS2GameHelper.Utils;

namespace CS2GameHelper.Data.Game;

public class GameData : ThreadedServiceBase
{
    #region properties

    protected override string ThreadName => nameof(GameData);

    private GameProcess? GameProcess { get; set; }

    public Player? Player { get; private set; }

    public Entity.Entity[]? Entities { get; private set; }

    #endregion

    #region methods

    /// <inheritdoc />
    public GameData(GameProcess gameProcess)
    {
        GameProcess = gameProcess;
        Player = new Player();
        Entities = Enumerable.Range(0, 64).Select(index => new Entity.Entity(index)).ToArray();
    }

    public override void Dispose()
    {
        base.Dispose();

        Entities = null;
        Player = null;
        GameProcess = null;
    }

    protected override void FrameAction()
    {
        if (GameProcess == null || !GameProcess.IsValid) return;
        if (Player != null) Player.Update(GameProcess);

        if (Entities != null)
            foreach (var entity in Entities)
                entity.Update(GameProcess);
    }

    #endregion
}
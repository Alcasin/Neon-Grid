using System;
using System.Collections.Generic;
using NeonGrid.Data;
using NeonGrid.Simulation;

namespace NeonGrid.Validation
{
    public sealed class LevelValidator
    {
        public LevelValidationResult Validate(LevelDefinition level)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            return Validate(level.Width, level.Height, level.Tiles);
        }

        public LevelValidationResult Validate(int width, int height, IReadOnlyList<TileDefinition> tiles)
        {
            var result = new LevelValidationResult();
            if (width <= 0)
                result.AddError(LevelValidationCode.InvalidWidth, $"Width must be positive; found {width}.");
            if (height <= 0)
                result.AddError(LevelValidationCode.InvalidHeight, $"Height must be positive; found {height}.");

            if (tiles == null)
            {
                result.AddError(LevelValidationCode.MissingTileRecords, "The tile definition list is missing.");
                result.AddError(LevelValidationCode.MissingPowerSource, "At least one PowerSource is required.");
                result.AddError(LevelValidationCode.MissingOutputLamp, "At least one OutputLamp is required.");
                return result;
            }

            long expectedTileCount = (long)width * height;
            if (width > 0 && height > 0 && tiles.Count != expectedTileCount)
            {
                result.AddError(LevelValidationCode.UnexpectedTileRecordCount,
                    $"Expected exactly {expectedTileCount} tile records for a {width}x{height} level; found {tiles.Count}.");
            }

            var occupied = new HashSet<GridPosition>();
            int sourceCount = 0;
            int lampCount = 0;
            for (int index = 0; index < tiles.Count; index++)
            {
                TileDefinition tile = tiles[index];
                if (tile == null)
                {
                    result.AddError(LevelValidationCode.NullTileDefinition,
                        $"Tile record {index} is null.");
                    continue;
                }

                bool inBounds = width > 0 && height > 0 &&
                                tile.position.x >= 0 && tile.position.x < width &&
                                tile.position.y >= 0 && tile.position.y < height;
                if (!inBounds)
                {
                    result.AddError(LevelValidationCode.TileOutOfBounds,
                        $"Tile record {index} at {tile.position} is outside the {width}x{height} grid.");
                }

                if (!occupied.Add(tile.position))
                {
                    result.AddError(LevelValidationCode.DuplicateTilePosition,
                        $"More than one tile record uses position {tile.position}.");
                }

                if (!Enum.IsDefined(typeof(TileType), tile.tileType))
                {
                    result.AddError(LevelValidationCode.InvalidTileType,
                        $"Tile at {tile.position} has unknown type value {(int)tile.tileType}.");
                    continue;
                }

                if (tile.startingRotation < 0 || tile.startingRotation > 3)
                {
                    result.AddError(LevelValidationCode.InvalidRotation,
                        $"Tile at {tile.position} has rotation {tile.startingRotation}; serialized rotations must be 0 through 3.");
                }

                if (tile.startingSwitchOn && tile.tileType != TileType.Switch)
                {
                    result.AddError(LevelValidationCode.SwitchStateOnNonSwitch,
                        $"Tile at {tile.position} is {tile.tileType} but has switch starting state enabled.");
                }

                if (tile.tileType == TileType.Empty)
                {
                    if (tile.isRotatable)
                    {
                        result.AddWarning(LevelValidationCode.IgnoredRotatableFlag,
                            $"Empty tile at {tile.position} is marked rotatable, but Empty has no supported interaction.");
                    }

                    if (tile.startingRotation != 0)
                    {
                        result.AddWarning(LevelValidationCode.IrrelevantEmptyRotation,
                            $"Empty tile at {tile.position} has a non-zero rotation that has no logical effect.");
                    }
                }
                else if (tile.tileType == TileType.Switch && tile.isRotatable)
                {
                    result.AddWarning(LevelValidationCode.IgnoredRotatableFlag,
                        $"Switch at {tile.position} is marked rotatable; its supported interaction is ToggleSwitch.");
                }

                if (tile.tileType == TileType.PowerSource) sourceCount++;
                if (tile.tileType == TileType.OutputLamp) lampCount++;
            }

            if (sourceCount == 0)
                result.AddError(LevelValidationCode.MissingPowerSource, "At least one PowerSource is required.");
            if (lampCount == 0)
                result.AddError(LevelValidationCode.MissingOutputLamp, "At least one OutputLamp is required.");

            return result;
        }

        public LevelValidationReport ValidateWithSolver(LevelDefinition level, PuzzleSolverOptions options = null)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            return ValidateWithSolver(level.Width, level.Height, level.Tiles, options);
        }

        public LevelValidationReport ValidateWithSolver(int width, int height,
            IReadOnlyList<TileDefinition> tiles, PuzzleSolverOptions options = null)
        {
            LevelValidationResult structural = Validate(width, height, tiles);
            if (!structural.IsValid)
                return new LevelValidationReport(structural, null);

            BoardState board;
            try
            {
                board = new BoardState(width, height, tiles);
            }
            catch (ArgumentException exception)
            {
                structural.AddError(LevelValidationCode.BoardConstructionFailed,
                    $"Board construction failed after validation: {exception.Message}");
                return new LevelValidationReport(structural, null);
            }

            PuzzleSolverResult solverResult = new PuzzleSolver().Solve(board,
                options ?? PuzzleSolverProfiles.AuthoringExact);
            return new LevelValidationReport(structural, solverResult);
        }
    }
}

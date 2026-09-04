using System.Collections.Generic;

namespace NeonGrid.Validation
{
    public enum ValidationSeverity
    {
        Warning,
        Error
    }

    public enum LevelValidationCode
    {
        InvalidWidth,
        InvalidHeight,
        MissingTileRecords,
        UnexpectedTileRecordCount,
        NullTileDefinition,
        TileOutOfBounds,
        DuplicateTilePosition,
        InvalidTileType,
        InvalidRotation,
        SwitchStateOnNonSwitch,
        IgnoredRotatableFlag,
        IrrelevantEmptyRotation,
        MissingPowerSource,
        MissingOutputLamp,
        BoardConstructionFailed
    }

    public sealed class LevelValidationIssue
    {
        public ValidationSeverity Severity { get; }
        public LevelValidationCode Code { get; }
        public string Message { get; }

        public LevelValidationIssue(ValidationSeverity severity, LevelValidationCode code, string message)
        {
            Severity = severity;
            Code = code;
            Message = message;
        }

        public override string ToString() => $"{Severity}: {Message}";
    }

    public sealed class LevelValidationResult
    {
        private readonly List<LevelValidationIssue> errors = new List<LevelValidationIssue>();
        private readonly List<LevelValidationIssue> warnings = new List<LevelValidationIssue>();

        public bool IsValid => errors.Count == 0;
        public IReadOnlyList<LevelValidationIssue> Errors => errors;
        public IReadOnlyList<LevelValidationIssue> Warnings => warnings;

        internal void AddError(LevelValidationCode code, string message)
        {
            errors.Add(new LevelValidationIssue(ValidationSeverity.Error, code, message));
        }

        internal void AddWarning(LevelValidationCode code, string message)
        {
            warnings.Add(new LevelValidationIssue(ValidationSeverity.Warning, code, message));
        }
    }
}

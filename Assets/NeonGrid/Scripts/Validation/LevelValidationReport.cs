using NeonGrid.Simulation;

namespace NeonGrid.Validation
{
    public sealed class LevelValidationReport
    {
        public LevelValidationResult StructuralValidation { get; }
        public PuzzleSolverResult SolverResult { get; }

        public LevelValidationReport(LevelValidationResult structuralValidation, PuzzleSolverResult solverResult)
        {
            StructuralValidation = structuralValidation;
            SolverResult = solverResult;
        }
    }
}

using DiffPlex.DiffBuilder.Model;

namespace DoctestCsharp
{
    public static class Report
    {
        public interface IReport
        {
            // Intentionally empty
        }

        public class Ok : IReport
        {
            // Intentionally empty
        }

        public class Different : IReport
        {
            public DiffPlex.DiffBuilder.Model.DiffPaneModel Diff;

            public Different(DiffPlex.DiffBuilder.Model.DiffPaneModel diff)
            {
                Diff = diff;
            }
        }

        public class DoesntExist : IReport
        {
            // Intentionally empty
        }

        public class ShouldNotExist : IReport
        {
            // Intentionally empty
        }
    }
}
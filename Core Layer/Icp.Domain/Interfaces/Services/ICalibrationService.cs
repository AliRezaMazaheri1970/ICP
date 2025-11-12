using Core.Icp.Domain.Entities.Elements;

namespace Core.Icp.Domain.Interfaces.Services
{
    /// <summary>
    /// Defines the contract for a service that handles element calibration processes.
    /// </summary>
    public interface ICalibrationService
    {
        /// <summary>
        /// Asynchronously creates a new calibration curve based on a set of standard points.
        /// </summary>
        /// <param name="elementId">The unique identifier of the element being calibrated.</param>
        /// <param name="projectId">The unique identifier of the project this calibration belongs to.</param>
        /// <param name="points">A collection of <see cref="CalibrationPoint"/> objects, each representing a known concentration and its measured intensity.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the newly created <see cref="CalibrationCurve"/>.</returns>
        Task<CalibrationCurve> CreateCalibrationCurveAsync(
            int elementId,
            int projectId,
            IEnumerable<CalibrationPoint> points);

        /// <summary>
        /// Asynchronously calculates the concentration of an unknown sample based on its measured intensity and a given calibration curve.
        /// </summary>
        /// <param name="calibrationCurveId">The unique identifier of the calibration curve to use for the calculation.</param>
        /// <param name="intensity">The measured signal intensity of the unknown sample.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the calculated concentration.</returns>
        Task<decimal> CalculateConcentrationAsync(
            int calibrationCurveId,
            decimal intensity);

        /// <summary>
        /// Asynchronously retrieves the currently active calibration curve for a specific element within a project.
        /// </summary>
        /// <param name="elementId">The unique identifier of the element.</param>
        /// <param name="projectId">The unique identifier of the project.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the active <see cref="CalibrationCurve"/> or null if none is found.</returns>
        Task<CalibrationCurve?> GetActiveCalibrationCurveAsync(
            int elementId,
            int projectId);

        /// <summary>
        /// Asynchronously evaluates the quality of a calibration curve, for example, by checking its R-squared value.
        /// </summary>
        /// <param name="curve">The <see cref="CalibrationCurve"/> to validate.</param>
        /// <returns>A task that represents the asynchronous operation. The task result is true if the calibration quality is acceptable; otherwise, false.</returns>
        Task<bool> ValidateCalibrationQualityAsync(CalibrationCurve curve);

        /// <summary>
        /// Asynchronously calculates the coefficient of determination (R²) for a given calibration curve.
        /// </summary>
        /// <param name="curve">The <see cref="CalibrationCurve"/> for which to calculate the R-squared value.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the calculated R-squared value.</returns>
        Task<decimal> CalculateRSquaredAsync(CalibrationCurve curve);
    }
}
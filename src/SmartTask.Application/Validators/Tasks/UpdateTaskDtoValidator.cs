using SmartTask.Application.DTOs.Tasks;

namespace SmartTask.Application.Validators.Tasks;

/// <summary>
/// Rules for <c>PUT tasks/{id}</c>: the shared task rules only. <c>DueDate</c> deliberately has no
/// past-date rule, so the title of a task that is already overdue can still be edited.
/// </summary>
public sealed class UpdateTaskDtoValidator : TaskDetailsValidator<UpdateTaskDto>;

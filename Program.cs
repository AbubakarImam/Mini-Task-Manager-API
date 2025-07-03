using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Rewrite;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<ITaskService, TaskService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var app = builder.Build();

// --- START: Middleware Configuration (Added Parts) ---

// Configure the HTTP request pipeline.

// Use CORS policy. This must be called before Map* methods.
//app.UseCors();

// Enable Swagger UI in development environment.
// This provides a web interface to explore and test your API.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();     // Serves the OpenAPI specification
    app.UseSwaggerUI();   // Serves the Swagger UI (web page)
}

// Enable HTTPS redirection.
// Redirects HTTP requests to HTTPS for security.
app.UseHttpsRedirection();
app.UseRewriter(new RewriteOptions().AddRedirect("todos/(.*)", "tasks/$1"));
app.Use(async (context, next) =>
{
   Console.WriteLine($"[{context.Request.Method} {context.Request.Path} {DateTime.UtcNow} Started]");
   await next(context);
   Console.WriteLine($"[{context.Request.Method} {context.Request.Path} {DateTime.UtcNow} Finished]");

});
// --- START: API Endpoints (Routing) for TaskItem (Added Parts) ---

// Group endpoints for better organization in Swagger UI and routing prefix
var tasksApi = app.MapGroup("/api/tasks"); // All routes will now start with /api/tasks

// 1. GET all tasks: /api/tasks
tasksApi.MapGet("/", async (ITaskService taskService) =>
{
    var tasks = await taskService.GetAllTasksAsync();
    return TypedResults.Ok(tasks); // Returns HTTP 200 OK with the list of tasks
})
.WithName("GetAllTasks"); // Optional: Gives a name to the endpoint (useful for linking/logging)
//.WithOpenApi();          // Required for Swagger to document this endpoint

// 2. GET task by ID: /api/tasks/{id}
tasksApi.MapGet("/{id:int}", async (int id, ITaskService taskService) =>
{
    var task = await taskService.GetTaskByIdAsync(id);
    if (task == null)
    {
        return Results.NotFound($"Task with ID {id} not found."); // Returns HTTP 404 Not Found
    }
    return Results.Ok(task); // Returns HTTP 200 OK with the specific task
})
.WithName("GetTaskById");
//.WithOpenApi();

// 3. POST new task: /api/tasks
tasksApi.MapPost("/", async ([FromBody] TaskItem task, ITaskService taskService) =>
{
    // Minimal APIs typically infer [FromBody] for complex types, but explicit is clear.
    // We expect the 'Id' to be 0 or irrelevant from the client, as the service generates it.
    var addedTask = await taskService.AddTaskAsync(task);

    // Return HTTP 201 Created status, with the URL to the new resource
    // and the newly created task object in the response body.
    return Results.Created($"/api/tasks/{addedTask.Id}", addedTask);
})
.AddEndpointFilter(async (context, next) =>
{
    var taskArgument = context.GetArgument<TaskItem>(0); // Get the TaskItem argument from the context
    var errors = new Dictionary<string, string[]>();
    if (taskArgument.DueDate < DateTime.UtcNow)
    {
    errors.Add("DueDate", new[] { "Due date cannot be in the past." });
    }
    if (taskArgument.IsCompleted)
    {
    errors.Add(nameof(TaskItem.IsCompleted), ["Cannot add completed task"]);
    }
    if (errors.Count > 0)
    {
        return TypedResults.ValidationProblem(errors);
    }
    return await next(context); // Proceed to the next filter or endpoint
})
.WithName("CreateTask");
//.WithOpenApi();

// 4. PUT update task: /api/tasks/{id}
tasksApi.MapPut("/{id:int}", async (int id, [FromBody] TaskItem updatedTask, ITaskService taskService) =>
{
    // Validate if the ID in the route matches the ID in the request body (optional but good practice)
    //if (id != updatedTask.Id)
    //{
    //    return Results.BadRequest("Task ID in route must match ID in body.");
    //}
    var taskWithCorrectId = updatedTask with { Id = id }; // Force ID
    var result = await taskService.UpdateTaskAsync(id, taskWithCorrectId);

    return result is null ? Results.NotFound($"Task with ID {id} not found.") : Results.Ok(result);


    //var result = await taskService.UpdateTaskAsync(id, updatedTask);
    //if (result == null)
    //{
    //    return Results.NotFound($"Task with ID {id} not found."); // Returns HTTP 404 Not Found
    //}
    //return Results.Ok(result); // Returns HTTP 200 OK with the updated task
})
.WithName("UpdateTask");
//.WithOpenApi();

// 5. DELETE task: /api/tasks/{id}
tasksApi.MapDelete("/{id:int}", async (int id, ITaskService taskService) =>
{
    var deletedTask = await taskService.DeleteTaskAsync(id);
    if (deletedTask == null)
    {
        return Results.NotFound($"Task with ID {id} not found."); // Returns HTTP 404 Not Found
    }
    // Returns HTTP 204 No Content (common for successful deletions where no body is needed)
    // or HTTP 200 OK with the deleted item (also common, depends on API design)
    return Results.Ok(deletedTask); // Or Results.NoContent();
})
.WithName("DeleteTask");
//.WithOpenApi();

// --- END: API Endpoints ---

app.Run(); // Starts the web application


// --- Existing Code (with minor corrections/comments) ---
public record TaskItem(
    int Id,
    string Title,
    string Description,
    DateTime DueDate,
    bool IsCompleted
    );

interface ITaskService
{
    Task<List<TaskItem>> GetAllTasksAsync();
    Task<TaskItem?> GetTaskByIdAsync(int id);
    Task<TaskItem> AddTaskAsync(TaskItem task);
    Task<TaskItem?> UpdateTaskAsync(int Id, TaskItem updatedTask);
    Task<TaskItem?> DeleteTaskAsync(int id);
}

class TaskService : ITaskService
{
    private readonly List<TaskItem> _tasks = [];
    private int _nextId = 1; // To manage IDs

    public async Task<List<TaskItem>> GetAllTasksAsync()
    {
        await Task.Delay(10);
        return _tasks.ToList();
    }

    public async Task<TaskItem?> GetTaskByIdAsync(int id)
    {
        await Task.Delay(0); // Simulate async operation
        return _tasks.FirstOrDefault(t => t.Id == id);
    }
    public async Task<TaskItem> AddTaskAsync(TaskItem task)
    {
        await Task.Delay(0); // Simulate async operation
        TaskItem newTask = task with { Id = _nextId++ };
        _tasks.Add(newTask);
        return newTask;
    }

    public async Task<TaskItem?> UpdateTaskAsync(int id, TaskItem updatedTask)
    {
        await Task.Delay(0); // Simulate async operation
        var existingTask = _tasks.FirstOrDefault(t => t.Id == id);
        if (existingTask == null)
        {
            return null; // Task not found
        }

        TaskItem taskToUpdate = updatedTask with { Id = id };
        _tasks.Remove(existingTask);
        _tasks.Add(taskToUpdate);
        return taskToUpdate;

    }

    public async Task<TaskItem?> DeleteTaskAsync(int id)
    {
        await Task.Delay(0); // Simulate async operation
        var taskToDelete = _tasks.FirstOrDefault(t => t.Id == id);
        if (taskToDelete == null)
        {
            return null; // Task not found
        }
        _tasks.Remove(taskToDelete);
        return taskToDelete;
    }
}
﻿using Application.Dtos;
using AutoMapper;
using DynamicExamSystem.infrastructure.Notification;
using DynamicExamSystem.infrastructure.repository.Interfaces;
using DynamicExamSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace DynamicExamSystem.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ExamController : ControllerBase
    {
        private readonly IExamRepository _examRepository;
        private readonly IExamResultRepository _examResultRepository;
        private readonly IMapper _mapper;
        private readonly IHubContext<NotificationHub> _hubContext;

        public ExamController(IExamRepository examRepository, IMapper mapper, IExamResultRepository examResultRepository, IHubContext<NotificationHub> hubContext)
        {
            _examRepository = examRepository;
            _mapper = mapper;
            _examResultRepository = examResultRepository;
            _hubContext = hubContext;
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<ActionResult<CreateExamDto>> CreateExam([FromBody] CreateExamDto examDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var exam = _mapper.Map<Exam>(examDto);
            await _examRepository.AddAsync(exam);
            await _examRepository.SaveChangesAsync();

            var createdExamDto = _mapper.Map<CreateExamDto>(exam);
            return Ok(createdExamDto);
        }

        [HttpGet("subject/{subjectId}")]
        public async Task<ActionResult<IEnumerable<ExamDto>>> GetExamsBySubjectId(int subjectId)
        {
            var exams = await _examRepository.GetExamsBySubjectIdAsync(subjectId);

            if (exams == null || !exams.Any())
            {
                return NotFound($"No exams found for SubjectId {subjectId}.");
            }

            var examDtos = _mapper.Map<IEnumerable<ExamDto>>(exams);
            return Ok(examDtos);
        }

        [HttpGet("{examId}/questions")]
        public async Task<ActionResult<IEnumerable<QuestionsDto>>> GetQuestionsInExam(int examId)
        {
            var exam = await _examRepository.GetExamByIdAsync(examId);

            if (exam == null)
            {
                return NotFound($"Exam with ID {examId} not found.");
            }

            var questionDtos = _mapper.Map<IEnumerable<QuestionsDto>>(exam.Questions);
            
            return Ok(questionDtos);
        }



        [Authorize(Roles = "Admin")]
        [HttpPost("{examId}/questions")]
        public async Task<ActionResult<QuestionDto>> AddQuestionToExam(int examId, [FromBody] QuestionDto questionDto)
        {
            var exam = await _examRepository.GetExamByIdAsync(examId);

            if (exam == null)
            {
                return NotFound($"Exam with ID {examId} not found.");
            }

            var question = _mapper.Map<Question>(questionDto);
            exam.Questions.Add(question);

            await _examRepository.SaveChangesAsync();

            var createdQuestionDto = _mapper.Map<QuestionDto>(question);
            return CreatedAtAction(nameof(GetQuestionsInExam), new { examId = examId }, createdQuestionDto);
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{examId}/questions/{questionId}")]
        public async Task<ActionResult<QuestionDto>> UpdateQuestionInExam(int examId, int questionId, [FromBody] QuestionEditDto questionDto)
        {
            var exam = await _examRepository.GetExamByIdAsync(examId);
            if (exam == null)
            {
                return NotFound("Exam not found.");
            }

            var question = await _examRepository.GetQuestionByIdAsync(questionId);
            if (question == null || question.ExamId != examId)
            {
                return NotFound("Question not found in this exam.");
            }

            _mapper.Map(questionDto, question);  

            await _examRepository.SaveChangesAsync();
            return Ok("The question was updated successfully.");
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{examId}")]
        public async Task<ActionResult> UpdateExamName(int examId, [FromBody] UpdateExamNameRequestDto request)
        {
            if (string.IsNullOrEmpty(request.NewName))
            {
                return BadRequest("Exam name cannot be empty.");
            }

            var exam = await _examRepository.GetExamByIdAsync(examId);
            if (exam == null)
            {
                return NotFound("Exam not found.");
            }

            exam.Title = request.NewName;

            await _examRepository.SaveChangesAsync();
            return Ok("Exam name updated successfully.");
        }

        // signalr 

        [Authorize(Roles = "Student")]
        [HttpPost("exam/{examId}/submit")]
        public async Task<ActionResult<ExamResultDto>> SubmitExamAnswers(int examId, [FromBody] List<AnswerSubmissionDto> answers)
        {
            foreach (var claim in User.Claims)
            {
                Console.WriteLine(claim);
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
                return Unauthorized("User not logged in.");

            var result = await _examRepository.EvaluateExamAsync(examId, userId, answers);

            await _hubContext.Clients.All.SendAsync("ExamSubmittedByStudent", new
            {
                Message = "A student has submitted their exam.",
                StudentId = userId,
                ExamId = examId,
                SubmissionTime = DateTime.UtcNow
            });

            return Ok(result);
        }



        [Authorize(Roles = "Student")]
        [HttpGet("exam/results")]
        public async Task<ActionResult<List<ExamResultDto>>> GetExamResults()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
                return Unauthorized("User not logged in.");

            var results = await _examResultRepository.GetStudentExamResultsAsync(userId);

            if (results == null || !results.Any())
                return NotFound("No exam results found for the user.");

            return Ok(results);
        }
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteExam(int id)
        {

            var exam = await _examRepository.GetExamByIdAsync(id);
            if (exam == null)
            {
                return NotFound(new { message = "Exam not found." });
            }

            await _examRepository.DeleteExamAsync(exam);
            await _examRepository.SaveChangesAsync();

            return Ok(new { message = "Exam deleted successfully." });
        }
        [Authorize(Roles = "Admin")]
        [HttpGet("history")]
        public async Task<ActionResult> GetAllUserHistory(int pageNumber = 1, int pageSize = 6)
        {
            var (histories, totalCount) = await _examResultRepository.GetAllStudentHistoryAsync(pageNumber, pageSize);

            if (histories == null || !histories.Any())
            {
                return NotFound("No student history found.");
            }

            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            return Ok(new
            {
                data = histories,
                totalCount,
                pageNumber,
                pageSize,
                totalPages
            });
        }



        [Authorize(Roles = "Student")]
        [HttpGet("history/{studentId}")]
        public async Task<ActionResult> GetUserHistory(string studentId, int pageNumber = 1, int pageSize = 6)
        {
            var (histories, totalCount) = await _examResultRepository.GetStudentHistoryByIdAsync(studentId, pageNumber, pageSize);

            if (histories == null || !histories.Any())
            {
                return NotFound("No student history found.");
            }

            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            return Ok(new
            {
                data = histories,
                totalCount,
                pageNumber,
                pageSize,
                totalPages
            });
        }


        [Authorize(Roles = "Admin")]
        [HttpDelete("DeleteQuestion/{questionId}")]
        public async Task<IActionResult> DeleteQuestionAsync(int questionId)
        {
            var question = await _examRepository.GetQuestionByIdAsync(questionId);
            if (question == null)
            {
                return NotFound("Question not found in this exam.");
            }
             await _examRepository.Remove(question);
             await _examRepository.SaveChangesAsync();

            return Ok("question deleted");
        }


    }
}

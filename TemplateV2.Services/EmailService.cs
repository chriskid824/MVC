using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using TemplateV2.Common.Extensions;
using TemplateV2.Infrastructure.Cache.Contracts;
using TemplateV2.Infrastructure.Configuration;
using TemplateV2.Infrastructure.Email;
using TemplateV2.Infrastructure.Email.Contracts;
using TemplateV2.Infrastructure.Repositories.ServiceRepos.EmailTemplateRepo.Contracts;
using TemplateV2.Infrastructure.Repositories.UnitOfWork.Contracts;
using TemplateV2.Models;
using TemplateV2.Models.DomainModels;
using TemplateV2.Models.EmailTemplates;
using TemplateV2.Models.ServiceModels.Email;
using TemplateV2.Services.Contracts;

namespace TemplateV2.Services
{
    public class EmailService : IEmailService
    {
        #region Instance Fields

        private readonly IEmailProvider _emailProvider;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IApplicationCache _cache;
        private readonly IUnitOfWorkFactory _uowFactory;
        private readonly IEmailTemplateRepo _emailTemplateRepo;

        #endregion

        #region Constructors

        public EmailService(
            IEmailProvider emailProvider,
            IHttpContextAccessor httpContextAccessor,
            IApplicationCache cache,
            IUnitOfWorkFactory uowFactory,
            IEmailTemplateRepo emailTemplateRepo)
        {
            _emailProvider = emailProvider;
            _httpContextAccessor = httpContextAccessor;
            _cache = cache;
            _uowFactory = uowFactory;
            _emailTemplateRepo = emailTemplateRepo;
        }

        #endregion

        #region Public Methods

        public async Task SendAccountActivation(SendAccountActivationRequest request)
        {
            var activationToken = string.Empty;

            UserEntity user;
            using (var uow = _uowFactory.GetUnitOfWork())
            {
                user = await uow.UserRepo.GetUserById(new Infrastructure.Repositories.DatabaseRepos.UserRepo.Models.GetUserByIdRequest()
                {
                    Id = request.UserId
                });

                activationToken = GenerateUniqueUserToken(uow);

                await uow.UserRepo.CreateUserToken(new Infrastructure.Repositories.DatabaseRepos.UserRepo.Models.CreateUserTokenRequest()
                {
                    User_Id = request.UserId,
                    Token = new Guid(activationToken),
                    Type_Id = (int)TokenTypeEnum.AccountActivation,
                    Created_By = ApplicationConstants.SystemUserId,
                });
                uow.Commit();
            }

            var configuration = await _cache.Configuration();
            var baseUrl = _httpContextAccessor.HttpContext.Request.GetBaseUrl();

            var templateHtml = await _emailTemplateRepo.GetForgotPasswordHTML();
            var template = new AccountActivationTemplate(templateHtml)
            {
                ActivationUrl = $"{baseUrl}/activate-account?token={activationToken}",
                ApplicationUrl = baseUrl
            };

            await _emailProvider.Send(new Infrastructure.Email.Models.SendRequest()
            {
                FromAddress = configuration.System_From_Email_Address,
                ToAddress = user.Email_Address,
                Subject = template.Subject,
                Body = template.GetHTMLContent()
            });
        }

        public async Task SendResetPassword(SendResetPasswordRequest request)
        {
            var resetToken = string.Empty;

            UserEntity user;
            using (var uow = _uowFactory.GetUnitOfWork())
            {
                user = await uow.UserRepo.GetUserById(new Infrastructure.Repositories.DatabaseRepos.UserRepo.Models.GetUserByIdRequest()
                {
                    Id = request.UserId
                });

                resetToken = GenerateUniqueUserToken(uow);

                await uow.UserRepo.CreateUserToken(new Infrastructure.Repositories.DatabaseRepos.UserRepo.Models.CreateUserTokenRequest()
                {
                    User_Id = request.UserId,
                    Token = new Guid(resetToken),
                    Type_Id = (int)TokenTypeEnum.ResetPassword,
                    Created_By = ApplicationConstants.SystemUserId,
                });
                uow.Commit();
            }

            var configuration = await _cache.Configuration();
            var baseUrl = _httpContextAccessor.HttpContext.Request.GetBaseUrl();

            var templateHtml = await _emailTemplateRepo.GetForgotPasswordHTML();
            var template = new ResetPasswordTemplate(templateHtml)
            {
                ResetPasswordUrl = $"{baseUrl}/reset-password?token={resetToken}",
                ApplicationUrl = baseUrl
            };

            await _emailProvider.Send(new Infrastructure.Email.Models.SendRequest()
            {
                FromAddress = configuration.System_From_Email_Address,
                ToAddress = user.Email_Address,
                Subject = template.Subject,
                Body = template.GetHTMLContent()
            });
        }

        public async Task SendContactMessage(SendContactMessageRequest request)
        {
            var configuration = await _cache.Configuration();
            var baseUrl = _httpContextAccessor.HttpContext.Request.GetBaseUrl();

            var templateHtml = await _emailTemplateRepo.GetContactMessageHTML();
            var template = new ContactMessageTemplate(templateHtml)
            {
                Name = request.Name,
                Message = request.Message,
                ApplicationUrl = baseUrl
            };

            await _emailProvider.Send(new Infrastructure.Email.Models.SendRequest()
            {
                FromAddress = request.EmailAddress,
                ToAddress = configuration.Contact_Email_Address,
                Subject = template.Subject,
                Body = template.GetHTMLContent()
            });
        }

        public async Task SendForgotPassword(SendForgotPasswordRequest request)
        {
            var resetToken = string.Empty;

            UserEntity user;
            using (var uow = _uowFactory.GetUnitOfWork())
            {
                user = await uow.UserRepo.GetUserByEmail(new Infrastructure.Repositories.DatabaseRepos.UserRepo.Models.GetUserByEmailRequest()
                {
                    Email_Address = request.EmailAddress
                });

                if (user == null)
                {
                    return;
                }

                resetToken = GenerateUniqueUserToken(uow);

                await uow.UserRepo.CreateUserToken(new Infrastructure.Repositories.DatabaseRepos.UserRepo.Models.CreateUserTokenRequest()
                {
                    User_Id = user.Id,
                    Token = new Guid(resetToken),
                    Type_Id = (int)TokenTypeEnum.ForgotPassword,
                    Created_By = ApplicationConstants.SystemUserId,
                });
                uow.Commit();
            }

            var configuration = await _cache.Configuration();
            var baseUrl = _httpContextAccessor.HttpContext.Request.GetBaseUrl();

            var templateHtml = await _emailTemplateRepo.GetForgotPasswordHTML();
            var template = new ForgotPasswordTemplate(templateHtml)
            {
                ResetPasswordUrl = $"{baseUrl}/Account/ResetPassword?token={resetToken}",
                ApplicationUrl = baseUrl
            };

            await _emailProvider.Send(new Infrastructure.Email.Models.SendRequest()
            {
                FromAddress = configuration.System_From_Email_Address,
                ToAddress = request.EmailAddress,
                Subject = template.Subject,
                Body = template.GetHTMLContent()
            });
        }

        #endregion

        #region Private Methods

        private string GenerateUniqueUserToken(IUnitOfWork uow)
        {
            var generatedCode = GenerateGuid();

            while (CheckUserTokenExists(uow, generatedCode))
            {
                generatedCode = GenerateGuid();
            }
            return generatedCode;
        }

        private string GenerateGuid()
        {
            return Guid.NewGuid().ToString("N");
        }

        private bool CheckUserTokenExists(IUnitOfWork uow, string token)
        {
            var tokenResult = uow.UserRepo.GetUserTokenByGuid(new Infrastructure.Repositories.DatabaseRepos.UserRepo.Models.GetUserTokenByGuidRequest()
            {
                Guid = new Guid(token)
            });
            tokenResult.Wait();
            return tokenResult.Result != null;
        }

        #endregion
    }
}

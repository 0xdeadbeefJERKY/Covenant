﻿// Author: Ryan Cobb (@cobbr_io)
// Project: Covenant (https://github.com/cobbr/Covenant)
// License: GNU GPLv3

using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

using Covenant.Core;
using Covenant.Models;
using Covenant.Models.Covenant;
using Covenant.Models.Listeners;
using Covenant.Models.Indicators;
using Encrypt = Covenant.Core.Encryption;

namespace Covenant.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]s")]
    public class ListenerController : Controller
    {
        private readonly CovenantContext _context;
        private readonly UserManager<CovenantUser> _userManager;
        private readonly SignInManager<CovenantUser> _signInManager;
        private readonly IConfiguration _configuration;
        // Dictionary of CancellationTokenSources for active listeners to stop them asynchronously
        private readonly Dictionary<int, CancellationTokenSource> _cancellationTokens;

        public ListenerController(CovenantContext context, UserManager<CovenantUser> userManager, SignInManager<CovenantUser> signInManager, IConfiguration configuration, Dictionary<int, CancellationTokenSource> cancellationTokens)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _cancellationTokens = cancellationTokens;
        }

        // GET: api/listeners/types
        // <summary>
        // Get listener types
        // </summary>
        [HttpGet("types", Name = "GetListenerTypes")]
        public IEnumerable<ListenerType> GetListenerTypes()
        {
            return _context.ListenerTypes.ToList();
        }

        // GET: api/listeners/types/{id}
        // <summary>
        // Get a listener type
        // </summary>
        [HttpGet("types/{id}", Name = "GetListenerType")]
        public ActionResult<ListenerType> GetListenerType(int id)
        {
            ListenerType type = _context.ListenerTypes.FirstOrDefault(LT => LT.Id == id);
            if (type == null)
            {
                return NotFound();
            }
            return Ok(type);
        }

        // GET: api/listeners
        // <summary>
        // Get listeners
        // </summary>
        [HttpGet(Name = "GetListeners")]
        public IEnumerable<Listener> GetListeners()
        {
            return _context.Listeners.ToList();
        }

        // GET: api/listeners/{id}
        // <summary>
        // Get a listener
        // </summary>
        [HttpGet("{id}", Name = "GetListener")]
        public ActionResult<Listener> GetListener(int id)
        {
            Listener listener = _context.Listeners.FirstOrDefault(L => L.Id == id);
            if (listener == null)
            {
                return NotFound();
            }
            return Ok(listener);
        }

        // PUT api/listeners
        // <summary>
        // Edit a Listener
        // </summary>
        [HttpPut(Name = "PutListener")]
        public ActionResult<Listener> PutHttpListener([FromBody] Listener listener)
        {
            Listener savedListener = _context.Listeners.FirstOrDefault(L => L.Id == listener.Id);
            if (savedListener == null)
            {
                return NotFound();
            }
            savedListener.Name = listener.Name;
            savedListener.Description = listener.Description;
            savedListener.BindAddress = listener.BindAddress;
            savedListener.BindPort = listener.BindPort;
            savedListener.ConnectAddress = listener.ConnectAddress;
            savedListener.CovenantToken = listener.CovenantToken;

            if (savedListener.Status == Listener.ListenerStatus.Active && listener.Status == Listener.ListenerStatus.Stopped)
            {
                savedListener.Stop(_cancellationTokens[savedListener.Id]);
                savedListener.Status = listener.Status;
            }
            else if (savedListener.Status != Listener.ListenerStatus.Active && listener.Status == Listener.ListenerStatus.Active)
            {
                HttpProfile profile = (HttpProfile)_context.Profiles.FirstOrDefault(HP => savedListener.ProfileId == HP.Id);
                if (profile == null)
                {
                    return NotFound();
                }
                CancellationTokenSource listenerCancellationToken = savedListener.Start(profile);
                if (listenerCancellationToken == null)
                {
                    return BadRequest();
                }
                _cancellationTokens[savedListener.Id] = listenerCancellationToken;
            }

            _context.Listeners.Update(savedListener);
            _context.SaveChanges();

            return Ok(listener);
        }

        // DELETE api/listeners/{id}
        // <summary>
        // Delete a Listener
        // </summary>
        [HttpDelete("{id}", Name = "DeleteListener")]
        public ActionResult<Listener> DeleteListener(int id)
        {
            Listener listener = _context.Listeners.FirstOrDefault(L => L.Id == id);
            if (listener == null)
            {
                return NotFound();
            }
            if (listener.Status == Listener.ListenerStatus.Active)
            {
                listener.Stop(_cancellationTokens[listener.Id]);
            }
            _context.Listeners.Remove(listener);
            _context.SaveChanges();
            return Ok(listener);
        }

        // GET api/listeners/http/{id}
        // <summary>
        // Get an already active HttpListener
        // </summary>
        [HttpGet("http/{id}", Name = "GetActiveHttpListener")]
        public ActionResult<HttpListener> GetActiveHttpListener(int id)
        {
            Listener listener = _context.Listeners.FirstOrDefault(L => L.Id == id);
            if (listener == null)
            {
                return NotFound();
            }
            ListenerType listenerType = _context.ListenerTypes.FirstOrDefault(L => L.Id == listener.ListenerTypeId);
            if (listenerType == null || listenerType.Name != "HTTP")
            {
                return NotFound();
            }
            return Ok((HttpListener)listener);
        }

        // POST api/listeners/http
        // <summary>
        // Create an HttpListener
        // </summary>
        [HttpPost("http", Name = "CreateHttpListener")]
        public async Task<ActionResult<HttpListener>> CreateHttpListener([FromBody] HttpListener listener = null)
        {
            if (listener == null || listener.Id == 0)
            {
                ListenerType httpType = _context.ListenerTypes.FirstOrDefault(LT => LT.Name == "HTTP");
                if (httpType == null)
                {
                    return BadRequest();
                }
                listener = (HttpListener) _context.Listeners.FirstOrDefault(L => L.ListenerTypeId == httpType.Id && L.Status == Listener.ListenerStatus.Uninitialized);
                if (listener != null)
                {
                    return Ok(listener);
                }
                else
                {
                    Profile profile = _context.Profiles.FirstOrDefault(HP => HP.Id == 1);
                    listener = new HttpListener(httpType.Id, profile.Id);
                }
            }

            // Append capital letter to appease Password complexity requirements, get rid of warning output
            string covenantListenerUsername = Utilities.CreateSecureGuid().ToString();
            string covenantListenerPassword = Utilities.CreateSecureGuid().ToString() + "A";
            CovenantUser covenantListenerUser = new CovenantUser { UserName = covenantListenerUsername };
            await _userManager.CreateAsync(covenantListenerUser, covenantListenerPassword);
            await _userManager.AddToRoleAsync(covenantListenerUser, "Listener");

            var signInResult = await _signInManager.PasswordSignInAsync(covenantListenerUser.UserName, covenantListenerPassword, true, false);
            var token = Utilities.GenerateJwtToken(
                covenantListenerUser.UserName, covenantListenerUser.Id, new[] { "Listener" },
                _configuration["JwtKey"], _configuration["JwtIssuer"],
                _configuration["JwtAudience"], _configuration["JwtExpireDays"]
            );
            listener.CovenantToken = token;

            _context.Listeners.Add(listener);
            _context.SaveChanges();
            return Ok(listener);
        }

        // PUT api/listeners/http
        // <summary>
        // Edit HttpListener
        // </summary>
        [HttpPut("http", Name = "PutHttpListener")]
        public ActionResult<HttpListener> PutHttpListener([FromBody] HttpListener httpListener)
        {
            Listener listener = _context.Listeners.FirstOrDefault(L => L.Id == httpListener.Id);
            if (listener == null)
            {
                return NotFound();
            }
            ListenerType listenerType = _context.ListenerTypes.FirstOrDefault(L => L.Id == listener.ListenerTypeId);
            if (listenerType == null || listenerType.Name != "HTTP")
            {
                return NotFound();
            }
            HttpListener savedhttpListener = (HttpListener)listener;

            // URL is calculated from BindAddress, BindPort, UseSSL components
            // Default to setting based on URL if requested URL differs
            if (savedhttpListener.Url != httpListener.Url)
            {
                savedhttpListener.Url = httpListener.Url;
            }
            else
            {
                savedhttpListener.BindAddress = httpListener.BindAddress;
                savedhttpListener.BindPort = httpListener.BindPort;
                savedhttpListener.ConnectAddress = httpListener.ConnectAddress;
                savedhttpListener.UseSSL = httpListener.UseSSL;
            }
            savedhttpListener.ProfileId = httpListener.ProfileId;
            savedhttpListener.Name = httpListener.Name;
            savedhttpListener.SSLCertificate = httpListener.SSLCertificate;

            if (savedhttpListener.Status == Listener.ListenerStatus.Active && httpListener.Status == Listener.ListenerStatus.Stopped)
            {
                savedhttpListener.Stop(_cancellationTokens[savedhttpListener.Id]);
                savedhttpListener.Status = httpListener.Status;
            }
            else if(savedhttpListener.Status != Listener.ListenerStatus.Active && httpListener.Status == Listener.ListenerStatus.Active)
            {
                if (savedhttpListener.UseSSL && (savedhttpListener.SSLCertHash == "" || savedhttpListener.SSLCertificate == ""))
                {
                    return BadRequest();
                }
                else if (_context.Listeners.Where(L => L.Status == Listener.ListenerStatus.Active && L.BindPort == listener.BindPort).Any())
                {
                    return BadRequest();
                }
                HttpProfile profile = (HttpProfile)_context.Profiles.FirstOrDefault(HP => HP.Id == savedhttpListener.ProfileId);
                CancellationTokenSource listenerCancellationToken = savedhttpListener.Start(profile);
                if (listenerCancellationToken == null)
                {
                    return BadRequest();
                }
                NetworkIndicator httpIndicator = new NetworkIndicator
                {
                    Protocol = "http",
                    Domain = Utilities.IsIPAddress(savedhttpListener.ConnectAddress) ? "" : savedhttpListener.ConnectAddress,
                    IPAddress = Utilities.IsIPAddress(savedhttpListener.ConnectAddress) ? savedhttpListener.ConnectAddress : "",
                    Port = savedhttpListener.BindPort,
                    URI = savedhttpListener.Url
                };
                if (_context.Indicators.Where(I => I.Name == "NetworkIndicator")
                    .Select(I => (NetworkIndicator)I)
                    .FirstOrDefault(I => I.IPAddress == httpIndicator.IPAddress && I.Domain == httpIndicator.Domain) == null)
                {
                    _context.Indicators.Add(httpIndicator);
                }
                _cancellationTokens[savedhttpListener.Id] = listenerCancellationToken;
            }

            _context.Listeners.Update(savedhttpListener);
            _context.SaveChanges();

            return Ok(listener);
        }

        // GET api/listeners/{id}/hostedfiles
        // <summary>
        // Get HostedFiles
        // </summary>
        [Authorize]
        [HttpGet("{id}/hostedfiles", Name = "GetHostedFiles")]
        public IEnumerable<HostedFile> GetHostedFiles(int id)
        {
            return _context.HostedFiles.Where(HF => HF.ListenerId == id).ToList();
        }

        // GET api/listeners/{id}/hostedfiles/{hfid}
        // <summary>
        // Get a HostedFile
        // </summary>
        [HttpGet("{id}/hostedfiles/{hfid}", Name = "GetHostedFile")]
        public ActionResult<HostedFile> GetHostedFile(int id, int hfid)
        {
            HostedFile file = _context.HostedFiles.FirstOrDefault(HF => HF.ListenerId == id && HF.Id == hfid);
            if (file == null)
            {
                return NotFound();
            }
            return Ok(file);
        }

        // POST api/listeners/{id}/hostedfiles
        // <summary>
        // Create a HostedFile
        // </summary>
        [HttpPost("{id}/hostedfiles", Name = "CreateHostedFile")]
        [ProducesResponseType(typeof(HostedFile), 201)]
        public ActionResult<HostedFile> CreateHostedFile(int id, [FromBody] HostedFile hostFileRequest)
        {
            HttpListener listener = (HttpListener)_context.Listeners.FirstOrDefault(L => L.Id == id);
            if (listener == null)
            {
                return NotFound();
            }
            hostFileRequest.ListenerId = listener.Id;
            HostedFile existingHostedFile = _context.HostedFiles.FirstOrDefault(HF => HF.Path == hostFileRequest.Path);
            if (existingHostedFile != null)
            {
                // If file already exists and is being hosted, BadRequest
                return BadRequest();
            }
            try
            {
                hostFileRequest = listener.HostFile(hostFileRequest);
            }
            catch
            {
                return BadRequest();
            }
            // Check if it already exists again, path could have changed
            existingHostedFile = _context.HostedFiles.FirstOrDefault(HF => HF.Path == hostFileRequest.Path);
            if (existingHostedFile != null)
            {
                return BadRequest();
            }
            _context.Indicators.Add(new FileIndicator
            {
                FileName = hostFileRequest.Path.Split("/").Last(),
                FilePath = listener.Url + hostFileRequest.Path,
                MD5 = Encrypt.Utilities.GetMD5(System.Convert.FromBase64String(hostFileRequest.Content)),
                SHA1 = Encrypt.Utilities.GetSHA1(System.Convert.FromBase64String(hostFileRequest.Content)),
                SHA2 = Encrypt.Utilities.GetSHA256(System.Convert.FromBase64String(hostFileRequest.Content))
            });
            _context.HostedFiles.Add(hostFileRequest);
            _context.SaveChanges();

            return CreatedAtRoute(nameof(GetHostedFile), new { id = listener.Id, hfid = hostFileRequest.Id }, hostFileRequest);
        }

        // PUT api/listeners/{id}/hostedfiles/{hfid}
        // <summary>
        // Edit HostedFile
        // </summary>
        [HttpPut("{id}/hostedfiles/{hfid}", Name = "EditHostedFile")]
        public ActionResult<HostedFile> EditHostedFile(int id, int hfid, [FromBody] HostedFile hostedFile)
        {
            HttpListener listener = (HttpListener)_context.Listeners.FirstOrDefault(L => L.Id == id);
            if (listener == null)
            {
                return NotFound();
            }
            HostedFile file = _context.HostedFiles.FirstOrDefault(HF => HF.ListenerId == listener.Id && HF.Id == hfid && HF.Id == hostedFile.Id);
            if (file == null)
            {
                return NotFound();
            }
            try
            {
                hostedFile = listener.HostFile(hostedFile);
            }
            catch
            {
                return BadRequest();
            }
            file.Path = hostedFile.Path;
            file.Content = hostedFile.Path;
            _context.HostedFiles.Update(file);
            _context.SaveChanges();

            return Ok(file);
        }

        // DELETE api/listeners/{id}/hostedfiles/{hfid}
        // <summary>
        // Delete a HostedFile
        // </summary>
        [HttpDelete("{id}/hostedfiles/{hfid}", Name = "DeleteHostedFile")]
        [ProducesResponseType(204)]
        public ActionResult DeleteHostedFile(int id, int hfid)
        {
            HttpListener listener = (HttpListener)_context.Listeners.FirstOrDefault(L => L.Id == id);
            if (listener == null)
            {
                return NotFound();
            }
            HostedFile file = _context.HostedFiles.FirstOrDefault(HF => HF.Id == hfid && HF.ListenerId == listener.Id);
            if (file == null)
            {
                return NotFound();
            }

            _context.HostedFiles.Remove(file);
            _context.SaveChanges();
            return new NoContentResult();
        }

        // GET api/listeners/{id}/profile
        // <summary>
        // Get a HttpProfile
        // </summary>
        [HttpGet("{id}/profile", Name = "GetListenerHttpProfile")]
        public ActionResult<HttpProfile> GetListenerHttpProfile(int id)
        {
            HttpListener listener = (HttpListener)_context.Listeners.FirstOrDefault(L => L.Id == id);
            if (listener == null)
            {
                return NotFound();
            }
            HttpProfile profile = (HttpProfile) _context.Profiles.FirstOrDefault(HP => HP.Id == listener.ProfileId);
            if (profile == null)
            {
                return NotFound();
            }
            return Ok(profile);
        }
    }
}

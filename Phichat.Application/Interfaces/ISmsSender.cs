﻿namespace Phichat.Application.Interfaces;

public interface ISmsSender
{
    Task SendAsync(string phoneNumber, string message);
}

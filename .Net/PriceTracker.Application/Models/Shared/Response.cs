using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceTracker.Application.Models.Shared
{
    public record ResponseDTO(
    bool Success,
    string Message);

    public record ResponseDTO<TData>(
        TData? Data,
        bool Success,
        string Message)
    {
        public static implicit operator ResponseDTO<TData>(ResponseDTO response)
            => new(default, response.Success, response.Message);
    }

    public static class Response
    {
        public static ResponseDTO IsSuccess(string? message = null)
            => new(
                Success: true,
                Message: string.IsNullOrWhiteSpace(message)
                    ? ErrorMessages.Success
                    : message);

        public static ResponseDTO<TData> IsSuccess<TData>(
            TData? data,
            string? message = null)
        {

            return new ResponseDTO<TData>(
                Data: data,
                Success: true,
                Message: string.IsNullOrWhiteSpace(message)
                    ? ErrorMessages.Success
                    : message);
        }

        public static ResponseDTO IsFailure(string? message = null)
        {
            return new ResponseDTO(
                Success: false,
                Message: string.IsNullOrWhiteSpace(message)
                    ? ErrorMessages.Fail
                    : message);
        }

        public static ResponseDTO<TData> IsFailure<TData>(
            TData? data,
            string? message = null)
        {
            return new ResponseDTO<TData>(
                Data: data,
                Success: false,
                Message: string.IsNullOrWhiteSpace(message)
                    ? ErrorMessages.Fail
                    : message);
        }
    }


}

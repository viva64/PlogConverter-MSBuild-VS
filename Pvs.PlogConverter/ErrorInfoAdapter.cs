//  2006-2008 (c) Viva64.com Team
//  2008-2020 (c) OOO "Program Verification Systems"
//  2020-2021 (c) PVS-Studio LLC
using System;
using ProgramVerificationSystems.PVSStudio.CommonTypes;

namespace ProgramVerificationSystems.PlogConverter
{
    public sealed class ErrorInfoAdapter : IEquatable<ErrorInfoAdapter>
    {
        public ErrorInfoAdapter()
            : this(new ErrorInfo())
        {
        }

        public ErrorInfoAdapter(ErrorInfo errorInfo)
        {
            ErrorInfo = errorInfo;
        }

        public ErrorInfo ErrorInfo { get; private set; }

        public bool Equals(ErrorInfoAdapter other)
        {
            if (other == null)
                return false;

            if (ErrorInfo.ErrorCode.Equals(other.ErrorInfo.ErrorCode, StringComparison.OrdinalIgnoreCase)
                && ErrorInfo.Message.Equals(other.ErrorInfo.Message, StringComparison.OrdinalIgnoreCase)
                && ErrorInfo.LineNumber == other.ErrorInfo.LineNumber
                && ErrorInfo.FileName.Equals(other.ErrorInfo.FileName, StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }

        public override bool Equals(object obj)
        {
            return obj is ErrorInfoAdapter && Equals((ErrorInfoAdapter)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ErrorInfo.ErrorCode.GetHashCode() ^ ErrorInfo.Message.GetHashCode()
                ^ ErrorInfo.LineNumber ^ ErrorInfo.FileName.GetHashCode();
            }
        }

        public static bool operator ==(ErrorInfoAdapter left, ErrorInfoAdapter right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(ErrorInfoAdapter left, ErrorInfoAdapter right)
        {
            return !Equals(left, right);
        }

        public override string ToString()
        {
            return String.Format("{0} {1}", ErrorInfo.ErrorCode, ErrorInfo.FileName); 
        }

        #region Additional properties for adoption

        public bool FavIcon { get; set; }
        public int DefaultOrder { get; set; }

        #endregion
    }
}
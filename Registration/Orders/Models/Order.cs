﻿using System;
using System.Collections.Generic;
using System.Linq;
using Conference.Common;
using ECommon.Utilities;
using ENode.Domain;
using Registration.SeatAssigning;

namespace Registration.Orders
{
    [Serializable]
    public class Order : AggregateRoot<Guid>
    {
        private OrderTotal _total;
        private Guid _conferenceId;
        private OrderStatus _status;
        private Registrant _registrant;
        private string _accessCode;

        public Order(Guid id, Guid conferenceId, IEnumerable<SeatQuantity> seats, Registrant registrant, IPricingService pricingService) : base(id)
        {
            Ensure.NotEmptyGuid(id, "id");
            Ensure.NotEmptyGuid(conferenceId, "conferenceId");
            Ensure.NotNull(seats, "seats");
            Ensure.NotNull(registrant, "registrant");
            Ensure.NotNull(pricingService, "pricingService");
            if (!seats.Any()) throw new ArgumentException("The seats of order cannot be empty.");

            var orderTotal = pricingService.CalculateTotal(conferenceId, seats);
            ApplyEvent(new OrderPlaced(this, conferenceId, orderTotal, registrant, DateTime.UtcNow.Add(ConfigSettings.ReservationAutoExpiration), ObjectId.GenerateNewStringId()));
        }

        public void ConfirmReservation(bool isReservationSuccess)
        {
            if (_status != OrderStatus.Placed)
            {
                throw new InvalidOperationException("Invalid order status:" + _status);
            }
            if (isReservationSuccess)
            {
                ApplyEvent(new OrderReservationConfirmed(this, _conferenceId, OrderStatus.ReservationSuccess));
            }
            else
            {
                ApplyEvent(new OrderReservationConfirmed(this, _conferenceId, OrderStatus.ReservationFailed));
            }
        }
        public void ConfirmPayment(bool isPaymentSuccess)
        {
            if (_status != OrderStatus.ReservationSuccess)
            {
                throw new InvalidOperationException("Invalid order status:" + _status);
            }
            if (isPaymentSuccess)
            {
                ApplyEvent(new OrderPaymentConfirmed(this, _conferenceId, OrderStatus.PaymentSuccess));
            }
            else
            {
                ApplyEvent(new OrderPaymentConfirmed(this, _conferenceId, OrderStatus.PaymentRejected));
            }
        }
        public void MarkAsSuccess()
        {
            if (_status != OrderStatus.PaymentSuccess)
            {
                throw new InvalidOperationException("Invalid order status:" + _status);
            }
            ApplyEvent(new OrderSuccessed(this, _conferenceId));
        }
        public void MarkAsExpire()
        {
            if (_status == OrderStatus.ReservationSuccess)
            {
                ApplyEvent(new OrderExpired(this, _conferenceId));
            }
        }
        public void Close()
        {
            if (_status != OrderStatus.ReservationSuccess && _status != OrderStatus.PaymentRejected)
            {
                throw new InvalidOperationException("Invalid order status:" + _status);
            }
            ApplyEvent(new OrderClosed(this, _conferenceId));
        }
        public SeatAssignments CreateSeatAssignments()
        {
            if (_status != OrderStatus.Success)
            {
                throw new InvalidOperationException("Cannot create seat assignments for an order that isn't success yet.");
            }
            return new SeatAssignments(_id, _total.Lines);
        }

        private void Handle(OrderPlaced evnt)
        {
            _id = evnt.AggregateRootId;
            _conferenceId = evnt.ConferenceId;
            _total = evnt.OrderTotal;
            _registrant = evnt.Registrant;
            _accessCode = evnt.AccessCode;
            _status = OrderStatus.Placed;
        }
        private void Handle(OrderReservationConfirmed evnt)
        {
            _status = evnt.OrderStatus;
        }
        private void Handle(OrderPaymentConfirmed evnt)
        {
            _status = evnt.OrderStatus;
        }
        private void Handle(OrderSuccessed evnt)
        {
            _status = OrderStatus.Success;
        }
        private void Handle(OrderExpired evnt)
        {
            _status = OrderStatus.Expired;
        }
        private void Handle(OrderClosed evnt)
        {
            _status = OrderStatus.Closed;
        }
    }
    public enum OrderStatus
    {
        Placed = 1,                //订单已生成
        ReservationSuccess,        //位置预定已成功（下单已成功）
        ReservationFailed,         //位置预定已失败（下单失败）
        PaymentSuccess,            //付款已成功
        PaymentRejected,           //付款已拒绝
        Expired,                   //订单已过期
        Success,                   //交易已成功
        Closed                     //订单已关闭
    }
}

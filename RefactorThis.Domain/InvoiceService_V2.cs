using RefactorThis.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RefactorThis.Domain
{
    public class InvoiceService_V2
    {
        private readonly InvoiceRepository _invoiceRepository;
        private const decimal TaxRate = 0.14m;

        public InvoiceService_V2(InvoiceRepository invoiceRepository)
        {
            _invoiceRepository = invoiceRepository;
        }

        public string ProcessPayment(Payment payment)
        {
            var invoice = _invoiceRepository.GetInvoice(payment.Reference);

            if (invoice == null)
                throw new InvalidOperationException("There is no invoice matching this payment.");

            string message = HandlePayment(invoice, payment);

            invoice.Save();
            return message;
        }

        private string HandlePayment(Invoice invoice, Payment payment)
        {
            if (invoice.Amount == 0)
                return HandleZeroAmountInvoice(invoice);

            if (invoice.Payments != null && invoice.Payments.Any())
                return ProcessExistingPayments(invoice, payment);

            return ProcessFirstPayment(invoice, payment);
        }

        private string HandleZeroAmountInvoice(Invoice invoice)
        {
            if (invoice.Payments == null || !invoice.Payments.Any())
                return "no payment needed";

            throw new InvalidOperationException(
                    "the invoice is in an invalid state, it has an amount of 0 and it has payments."
                );
        }

        private string ProcessExistingPayments(Invoice invoice, Payment payment)
        {
            decimal totalPaidAmount = invoice.Payments.Sum(x => x.Amount);
            decimal remainingAmount = invoice.Amount - invoice.AmountPaid;

            if (totalPaidAmount != 0 && invoice.Amount == totalPaidAmount)
                return "invoice was already fully paid";

            if (totalPaidAmount != 0 && payment.Amount > remainingAmount)
                return "the payment is greater than the partial amount remaining";

            return ApplyAdditionalPayment(invoice, payment, remainingAmount);
        }

        private string ProcessFirstPayment(Invoice invoice, Payment payment)
        {
            if (payment.Amount > invoice.Amount)
                return "the payment is greater than the invoice amount";

            return ApplyFirstPayment(invoice, payment);
        }

        private string ApplyAdditionalPayment(Invoice invoice, Payment payment, decimal remainingAmount)
        {
            ApplyPayment(invoice, payment, true);

            return (payment.Amount == remainingAmount
                    ? "final partial payment received, invoice is now fully paid"
                    : "another partial payment received, still not fully paid"
                );
        }

        private string ApplyFirstPayment(Invoice invoice, Payment payment)
        {
            ApplyPayment(invoice, payment, false);

            return (invoice.Amount == payment.Amount
                    ? "invoice is now fully paid"
                    : "invoice is now partially paid"
                );
        }

        private void ApplyPayment(Invoice invoice, Payment payment, bool isAdditionalPayment)
        {
            if (invoice.Type != InvoiceType.Standard && invoice.Type != InvoiceType.Commercial)
                throw new ArgumentOutOfRangeException(
                        nameof(invoice.Type),
                        invoice.Type,
                        "unsupported invoice type"
                    );

            if (isAdditionalPayment)
                invoice.AmountPaid += payment.Amount;
            else
                invoice.AmountPaid = payment.Amount;

            if (invoice.Type == InvoiceType.Commercial)
            {
                if (isAdditionalPayment)
                    invoice.TaxAmount += payment.Amount * TaxRate;
                else
                    invoice.TaxAmount = payment.Amount * TaxRate;
            }

            invoice.Payments.Add(payment);
        }
    }
}
